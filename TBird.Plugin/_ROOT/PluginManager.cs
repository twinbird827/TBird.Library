using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TBird.Core;

namespace TBird.Plugin
{
	public class PluginManager : IDisposable
	{
		/// <summary>
		/// ﾌﾟﾗｸﾞｲﾝDLLを配置するﾃﾞｨﾚｸﾄﾘ
		/// </summary>
		private const string _dllroot = "plugins";

		/// <summary>
		/// ﾏﾈｰｼﾞｬｲﾝｽﾀﾝｽ
		/// </summary>
		public static PluginManager Instance
		{
			get => _Instance = _Instance ?? new PluginManager();
		}
		private static PluginManager _Instance;

		/// <summary>
		/// ﾌﾟﾗｸﾞｲﾝﾘｽﾄ
		/// </summary>
		private List<PluginExecuter> _plugins = new List<PluginExecuter>();

		/// <summary>
		/// ﾏﾈｰｼﾞｬの初期化処理を実行します。
		/// </summary>
		public void Initialize()
		{
			var iplugin = typeof(IPlugin).FullName;
			var dllroot = Directories.GetAbsolutePath(_dllroot);

			try
			{
				if (!Directory.Exists(dllroot)) return;

				foreach (var dll in DirectoryUtil.GetFiles(dllroot, "*.dll"))
				{
					var asm = Assembly.LoadFrom(dll);

					asm.GetTypes()
						.Where(t => t.IsClass && t.IsPublic && !t.IsAbstract && t.GetInterface(iplugin) != null)
						.ForEach(t => _plugins.Add(new PluginExecuter(asm, t)));
				}
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
			}
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
					_plugins.ForEach(x => x.Dispose());
					_plugins.Clear();
				}

				// TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
				// TODO: 大きなフィールドを null に設定します。

				disposedValue = true;
			}
		}

		// TODO: 上の Dispose(bool disposing) にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
		// ~PluginManager() {
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