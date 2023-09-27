using System;
using TBird.Core.Utils;

namespace TBird.Core
{
	public abstract class TBirdObject : IDisposable, ILocker
	{
		/// <summary>
		/// GUID
		/// </summary>
		public string Lock
		{
			get => _Guid = _Guid ?? Locker.GetNewLockKey(this);
			set => _Guid = value;
		}
		private string _Guid;

		/// <summary>
		/// ｲﾝｽﾀﾝｽの文字列表現を取得します。
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return $"{base.ToString()} {Lock}";
		}

		/// <summary>
		/// ｲﾝｽﾀﾝｽと指定した別のBindableBaseの値が同値か比較します。
		/// </summary>
		/// <param name="obj">比較対象のｲﾝｽﾀﾝｽ</param>
		/// <returns></returns>
		public override bool Equals(object obj)
		{
			return obj is TBirdObject disposable && disposable != null
				? Lock.Equals(disposable.Lock)
				: false;
		}

		/// <summary>
		/// このｲﾝｽﾀﾝｽのﾊｯｼｭｺｰﾄﾞを返却します。
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			return Lock.GetHashCode();
		}

		/// <summary>
		/// ｲﾝｽﾀﾝｽ破棄時のｲﾍﾞﾝﾄ
		/// </summary>
		public event EventHandler Disposed;

		/// <summary>
		/// ｲﾝｽﾀﾝｽ破棄時ｲﾍﾞﾝﾄを追加します。
		/// </summary>
		/// <param name="bindable">一緒に追加するｲﾝｽﾀﾝｽ</param>
		/// <param name="handler">破棄ｲﾍﾞﾝﾄ</param>
		public void AddDisposed(EventHandler handler)
		{
			// ｲﾝｽﾀﾝｽ破棄ｲﾍﾞﾝﾄ自体を破棄するﾊﾝﾄﾞﾗを作成する
			EventHandler disposed = null; disposed = (sender, e) =>
			{
				Disposed -= handler;
				Disposed -= disposed;
			};

			Disposed -= handler;
			Disposed += handler;
			Disposed -= disposed;
			Disposed += disposed;
		}

		public bool IsDisposed { get; private set; } = false;

		protected virtual void DisposeManagedResource()
		{
			EventUtil.Raise(Disposed, this);
			Disposed = null;
			Locker.Dispose(Lock);
		}

		protected virtual void DisposeUnmanagedResource()
		{

		}

		protected void Dispose(bool disposing)
		{
			if (!IsDisposed)
			{
				if (disposing)
				{
					// TODO: マネージド状態を破棄します (マネージド オブジェクト)
					DisposeManagedResource();
				}

				// TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
				// TODO: 大きなフィールドを null に設定します
				DisposeUnmanagedResource();
				IsDisposed = true;
			}
		}

		// // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
		// ~Disposable()
		// {
		//     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// すでに破棄されている場合、例外を発生させます。
		/// </summary>
		public void ThrowIfDisposed()
		{
			if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
		}

		protected T[] Arr<T>(params T[] arr)
		{
			return arr;
		}
	}
}