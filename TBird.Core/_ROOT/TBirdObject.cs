using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TBird.Core.Utils;

namespace TBird.Core
{
	public abstract class TBirdObject : IDisposable
	{
		private Locker _lock = Locker.Create();

		public int WaitingCount => _lock.WaitingCount;

		public Task<IDisposable> LockAsync() => _lock.LockAsync();

		public void LockSync(Action action) => _lock.LockSync(action);

		public T LockSync<T>(Func<T> func) => _lock.LockSync(func);

		/// <summary>
		/// GUID
		/// </summary>
		public string Guid
		{
			get => _Guid = _Guid ?? GetLockString();
			set => _Guid = value;
		}
		private string? _Guid;

		protected virtual string GetLockString() => $"{GetType().FullName}-{System.Guid.NewGuid().ToString()}";

		/// <summary>
		/// ｲﾝｽﾀﾝｽの文字列表現を取得します。
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return $"{base.ToString()} {Guid}";
		}

		/// <summary>
		/// ｲﾝｽﾀﾝｽと指定した別のBindableBaseの値が同値か比較します。
		/// </summary>
		/// <param name="obj">比較対象のｲﾝｽﾀﾝｽ</param>
		/// <returns></returns>
		public override bool Equals(object obj)
		{
			return obj is TBirdObject disposable && disposable != null
				? Guid.Equals(disposable.Guid)
				: false;
		}

		/// <summary>
		/// このｲﾝｽﾀﾝｽのﾊｯｼｭｺｰﾄﾞを返却します。
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			return Guid.GetHashCode();
		}

		/// <summary>
		/// ｲﾝｽﾀﾝｽ破棄時のｲﾍﾞﾝﾄ
		/// </summary>
		public event EventHandler? Disposed;

		/// <summary>
		/// ｲﾝｽﾀﾝｽ破棄時ｲﾍﾞﾝﾄを追加します。
		/// </summary>
		/// <param name="bindable">一緒に追加するｲﾝｽﾀﾝｽ</param>
		/// <param name="handler">破棄ｲﾍﾞﾝﾄ</param>
		public void AddDisposed(EventHandler handler)
		{
			// ｲﾝｽﾀﾝｽ破棄ｲﾍﾞﾝﾄ自体を破棄するﾊﾝﾄﾞﾗを作成する
			EventHandler? disposed = null; disposed = (sender, e) =>
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
			if (Disposed != null)
			{
				EventUtil.Raise(Disposed, this);
				Disposed = null;
			}
			_lock.Dispose();
		}

		protected virtual void DisposeUnmanagedResource()
		{

		}

		protected void Dispose(bool disposing)
		{
			if (!IsDisposed)
			{
				IsDisposed = true;

				if (disposing)
				{
					// TODO: マネージド状態を破棄します (マネージド オブジェクト)
					DisposeManagedResource();
				}

				// TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
				// TODO: 大きなフィールドを null に設定します
				DisposeUnmanagedResource();
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

		protected IEnumerable<T> ArrMany<T>(IEnumerable<T>[] arr)
		{
			return arr.SelectMany(x => x);
		}

		protected T[] Arr<T>(params T[] arr)
		{
			return arr;
		}

		protected decimal Calc(object x, object y, Func<decimal, decimal, decimal> func)
		{
			return func((decimal)x.GetDouble(), (decimal)y.GetDouble());
		}
	}
}