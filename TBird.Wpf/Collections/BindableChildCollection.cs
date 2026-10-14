using TBird.Core;
using System.Collections.Specialized;
using System.Linq;

namespace TBird.Wpf.Collections
{
	public abstract class BindableChildCollection<T> : BindableCollection<T>, IBindableChild
	{
		public IBindableCollection Parent { get; private set; }

		protected BindableChildCollection(IBindableCollection collection, bool disposesource) : base(disposesource)
		{
			Parent = collection;
			LockObject = ((BindableCollection)Parent).LockObject;

			Parent.AddDisposed((sender, e) =>
			{
				Dispose();
			});
		}

		/// <summary>
		/// CollectionChangedにｲﾍﾞﾝﾄを追加します。
		/// </summary>
		/// <param name="notify">INotifyCollectionChangedを実装したﾘｽﾄｲﾝｽﾀﾝｽ</param>
		/// <param name="handler">ｲﾍﾞﾝﾄ</param>
		protected void AddBindableCollectionChanged(NotifyCollectionChangedEventHandler handler)
		{
			((BindableCollection)Parent).BindableCollectionChanged -= handler;
			((BindableCollection)Parent).BindableCollectionChanged += handler;

			AddDisposed((sender, e) =>
			{
				((BindableCollection)Parent).BindableCollectionChanged -= handler;
			});
		}

		protected override void DisposeManagedResource()
		{
			base.DisposeManagedResource();

			if (Parent is IBindableChild i)
			{
				i.Dispose();
			}
			Parent = null;
		}
	}
}