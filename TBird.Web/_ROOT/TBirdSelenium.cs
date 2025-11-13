using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Web
{
	public class TBirdSelenium : TBirdObject, IDisposable
	{
		/// <summary>ﾌﾞﾗｳｻﾞ</summary>
		internal ChromeDriver _driver;

		/// <summary>初期化処理</summary>
		private Action<ChromeDriver> _initialize;

		/// <summary>実行中かどうか</summary>
		public bool Executing { get; private set; }

		public TBirdSelenium()
		{
			AddDisposed((sender, e) =>
			{
				DriverDispose();
			});
		}

		private void DriverDispose()
		{
			if (_driver != null)
			{
				_driver.Quit();
				_driver.Dispose();
				_driver = null;
			}
		}

		/// <summary>
		/// ﾌﾞﾗｳｻﾞを作成します。
		/// </summary>
		/// <param name="hide">非表示状態にするかどうか</param>
		/// <returns></returns>
		private ChromeDriver CreateDriver(bool hide)
		{
			var service = ChromeDriverService.CreateDefaultService();
			var options = new ChromeOptions();

			if (hide)
			{
				service.HideCommandPromptWindow = true;

				options.AddArgument("--headless");
				options.AddArgument("--no-sandbox");
				options.AddArgument("--window-position=-32000,-32000");
				options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");
			}

			return new ChromeDriver(service, options);
		}

		/// <summary>
		/// ﾌﾞﾗｳｻﾞ作成時の初期化処理(ﾛｸﾞｲﾝ処理等)を設定します。
		/// </summary>
		/// <param name="action">ﾌﾞﾗｳｻﾞ作成時の初期化処理</param>
		public void SetInitialize(Action<ChromeDriver> action)
		{
			_initialize = action;
		}

		/// <summary>
		/// ﾌﾞﾗｳｻﾞ処理を実行します。
		/// </summary>
		/// <typeparam name="T">返却される値の型</typeparam>
		/// <param name="func">ﾌﾞﾗｳｻﾞ処理</param>
		/// <returns></returns>
		public async Task<T> Execute<T>(Func<ChromeDriver, T> func)
		{
			using (await Locker.LockAsync(Lock))
			using (this.Disposer(x => x.Executing = false))
			{
				Executing = true;

				for (var i = 0; i < 5; i++)
				{
					try
					{
						if (_driver == null)
						{
							_driver = CreateDriver(true);

							if (_initialize != null)
							{
								_initialize(_driver);
							}
						}

						return func(_driver);
					}
					catch (Exception ex)
					{
						// ｴﾗｰﾒｯｾｰｼﾞ表示
						MessageService.Debug(ex.ToString());
						// ﾄﾞﾗｲﾊﾞｰﾘｾｯﾄ
						DriverDispose();
						// 5秒待機してﾘﾄﾗｲ
						await Task.Delay(5000);
					}
				}
				throw new WebDriverTimeoutException("The process was not completed despite retrying the specified number of times.");
			}
		}
	}

	public static class TBirdSeleniumFactory
	{
		/// <summary>ﾌﾞﾗｳｻﾞ作成用ﾛｯｸ文字</summary>
		private static string _lock = Guid.NewGuid().ToString();

		/// <summary>作成済のﾌﾞﾗｳｻﾞを保管するためのﾘｽﾄ</summary>
		private static List<TBirdSelenium> _list = new List<TBirdSelenium>();

		/// <summary>
		/// ﾌﾞﾗｳｻﾞを作成します。
		/// </summary>
		/// <param name="pararell">並列処理数</param>
		/// <returns></returns>
		public static async Task<TBirdSelenium> CreateSelenium(int pararell)
		{
			using (await Locker.LockAsync(_lock))
			{
				// 実行中ではないﾌﾞﾗｳｻﾞを取得し、取得出来たら返却する。
				var instance = _list.FirstOrDefault(x => !x.Executing);
				if (instance != null) return instance;

				// 作成済のﾌﾞﾗｳｻﾞ数が規定数を超えていない場合はｲﾝｽﾀﾝｽ作成
				if (_list.Count < pararell)
				{
					var newinstance = new TBirdSelenium();
					_list.Add(newinstance);
					return newinstance;
				}

				// 実行完了するまで待機する。
				await TaskUtil.Delay(() => _list.FirstOrDefault(x => !x.Executing) != null);
				// 返却
				return _list.First(x => !x.Executing);
			}
		}

		public static Disposer<object> GetDisposer()
		{
			return new object().Disposer(_ =>
			{
				_list.ForEach(x => x.Dispose());
				_list.Clear();
			});
		}
	}

	public static class TBirdSeleniumExtension
	{
		/// <summary>
		/// ﾍﾟｰｼﾞ遷移が完了するまで待機します。
		/// </summary>
		/// <param name="sel">ﾌﾞﾗｳｻﾞ操作ｲﾝｽﾀﾝｽ</param>
		/// <returns></returns>
		public static void GoToUrl(this TBirdSelenium sel, string url)
		{
			for (var i = 0; i < 5; i++)
			{
				sel._driver.Navigate().GoToUrl(url);
				var wait = new WebDriverWait(sel._driver, TimeSpan.FromMilliseconds(10));
				var until = wait.Until(e =>
				{
					try
					{
						return e.FindElement(By.TagName(@"html"));
					}
					catch (Exception ex)
					{
						MessageService.Exception(ex);
						return null;
					}
				});
				if (until != null) return;
			}
		}
	}
}