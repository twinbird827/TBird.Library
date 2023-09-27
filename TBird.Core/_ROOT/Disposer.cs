using System;

namespace TBird.Core
{
	public class Disposer<T> : IDisposable
	{
		protected T _value;
		private Action<T> _dispose;

		/// <summary>
		/// <see cref="IDisposable"/> を継承しないｸﾗｽで疑似的にusing句を利用するためのｲﾝｽﾀﾝｽを生成します。
		/// </summary>
		/// <param name="value"><see cref="IDisposable"/> を継承しないｸﾗｽ</param>
		/// <param name="dispose"><see cref="Dispose"/>で行いたい処理</param>
		public Disposer(T value, Action<T> dispose)
		{
			_value = value;
			_dispose = dispose;
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
					_dispose(_value);

					_dispose = null;
				}

				// TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
				// TODO: 大きなフィールドを null に設定します。

				disposedValue = true;
			}
		}

		// TODO: 上の Dispose(bool disposing) にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
		// ~Disposer() {
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