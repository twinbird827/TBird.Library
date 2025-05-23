using System;
using System.Reflection;
using TBird.Core;

namespace TBird.Plugin
{
	public class PluginExecuter : IDisposable
	{
		/// <summary>
		/// 管理するﾌﾟﾗｸﾞｲﾝ
		/// </summary>
		private IPlugin _plugin;

		/// <summary>
		/// ﾀｲﾏｰ
		/// </summary>
		private IntervalTimer _timer;

		/// <summary>
		/// ﾌﾟﾗｸﾞｲﾝを初期化します。
		/// </summary>
		/// <param name="asm">ﾌﾟﾗｸﾞｲﾝを保持するｱｾﾝﾌﾞﾘ</param>
		/// <param name="type">ﾌﾟﾗｸﾞｲﾝのｸﾗｽ情報</param>
		public PluginExecuter(Assembly asm, Type type)
		{
			_plugin = asm.CreateInstance(type.FullName) as IPlugin
				?? throw new DllNotFoundException("The class that implements the plugin was not found.");

			_plugin.Initialize();

			_timer = new IntervalTimer(_plugin.Run);
			_timer.Interval = TimeSpan.FromMilliseconds(_plugin.Interval);
			_timer.Start();
		}

		#region IDisposable Support

		private bool disposedValue = false; // 重複する呼び出しを検出するには

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: マネージド状態を破棄します (マネージド オブジェクト)。
					if (_timer != null)
					{
						_timer.Stop();
						_timer.Dispose();
						_timer = null;
					}
					if (_plugin != null)
					{
						_plugin.Dispose();
						_plugin = null;
					}
				}

				// TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
				// TODO: 大きなフィールドを null に設定します。

				disposedValue = true;
			}
		}

		// TODO: 上の Dispose(bool disposing) にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
		// ~PluginExecuter() {
		//   // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
		//   Dispose(false);
		// }

		// このコードは、破棄可能なパターンを正しく実装できるように追加されました。
		public void Dispose()
		{
			// このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
			Dispose(true);
			// TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
			// GC.SuppressFinalize(this);
		}

		#endregion

	}
}