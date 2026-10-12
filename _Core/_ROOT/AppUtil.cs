using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Web;

namespace Netkeiba
{
	public static class AppUtil
	{
		public static string Sqlitepath { get; } = PathSetting.GetPath(@"database", "database.sqlite3");

		public static SQLiteControl CreateSQLiteControl() => new SQLiteControl(Sqlitepath, string.Empty, false, false, 1024 * 1024, true);

		public static async Task<IHtmlDocument> GetDocument(bool login, string url)
		{
			if (_loginsession.AddMinutes(10) < DateTime.Now)
			{
				if (_logincontext != null) _logincontext.Dispose();
				_logincontext = null;
				if (_guestcontext != null) _guestcontext.Dispose();
				_guestcontext = null;
				_loginsession = DateTime.Now;
			}

			var config = Configuration.Default.WithDefaultLoader().WithJs().WithDefaultCookies();

			if (login)
			{
				if (_logincontext == null)
				{
					var logincontext = BrowsingContext.New(config);

					await logincontext.OpenAsync(@"https://regist.netkeiba.com/account/?pid=login");

					if (logincontext.Active == null) throw new ApplicationException();

					await logincontext.Active.QuerySelectorAll<IHtmlFormElement>("form").First(x => x.QuerySelector("input[name='login_id']") != null).SubmitAsync(new
					{
						login_id = AppSetting.Instance.NetkeibaId,
						pswd = AppSetting.Instance.NetkeibaPassword
					});

					// 送信完了前に代入すると、失敗時に未ログインの context が以後の login 経路で使い回される
					_logincontext = logincontext;
				}
			}
			else
			{
				if (_guestcontext == null)
				{
					_guestcontext = BrowsingContext.New(config);
				}
			}

			var context = login ? _logincontext : _guestcontext;

			if (context == null) throw new ApplicationException("");

			using (await _lock.LockAsync())
			{
				await Task.Delay(1250);

				// TODO MainViewModel.AddLog($"req: {url}");
				MessageService.Debug($"req: {url}");

				return await context.OpenAsync(url).RunAsync(x => ((x.DocumentElement as IHtmlDocument) ?? x as IHtmlDocument).NotNull());
			}
		}

		private static int _pararell = 1;

		/// <summary>多重起動抑止ﾛｯｸ</summary>
		private static Locker _lock = Locker.Create(_pararell);

		private static IBrowsingContext? _logincontext;
		private static DateTime _loginsession = DateTime.Now.AddDays(-1);
		private static IBrowsingContext? _guestcontext;

	}
}