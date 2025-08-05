using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using TBird.Core;

namespace TBird.Wpf.Collections
{
	public class BindableDistinctCollection<T> : BindableChildCollection<T>
	{
		private Func<T, T, int> _func;
		private string[] _names;

		internal BindableDistinctCollection(BindableCollection<T> collection, Func<T, T, int> func, params string[] names) : base(collection)
		{
			_func = func;
			_names = names;

			collection.ForEach(Add);

			AddCollectionChanged(collection, (sender, e) =>
			{
				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						Add((T)e.NewItems[0]);
						break;
					case NotifyCollectionChangedAction.Remove:
						Remove((T)e.OldItems[0]);
						break;
					case NotifyCollectionChangedAction.Replace:
						Remove((T)e.OldItems[0]);
						Add((T)e.NewItems[0]);
						break;
					case NotifyCollectionChangedAction.Reset:
						Clear();
						break;
					case NotifyCollectionChangedAction.Move:
						throw new NotSupportedException("NotifyCollectionChangedAction is Move.");
				}
			});
		}

		public override void Add(T item)
		{
			if (item is IBindable bindable) AddOnRefreshCollection(bindable);
			if (this.Any(x => _func(item, x) == 0)) return;
			base.Add(item);
		}

		public override void Insert(int index, T item)
		{
			throw new NotSupportedException(nameof(Insert));
		}

		public override void RemoveAt(int index)
		{
			throw new NotSupportedException(nameof(RemoveAt));
		}

		public BindableDistinctCollection<T> AddOnRefreshCollection(IBindable bindable)
		{
			bindable.AddOnPropertyChanged(this, OnPropertyChangedRefreshCollection);
			return this;
		}

		private void OnPropertyChangedRefreshCollection(object sender, PropertyChangedEventArgs e)
		{
			if (!_names.Contains(e.PropertyName)) return;

			lock (_lock)
			{
				if (Parent is BindableCollection<T> parent)
				{
					// 重複を改めて削除
					Clear();

					// 親から追加
					parent.ForEach(Add);
				}
			}
		}

		private object _lock = new object();
	}

	public static class BindableDistinctCollectionExtension
	{
		public static BindableDistinctCollection<T> ToBindableDistinctCollection<T>(this BindableCollection<T> collection, Func<T, T, int> func, params string[] names)
		{
			return new BindableDistinctCollection<T>(collection, func, names);
		}

		public static BindableDistinctCollection<T> ToBindableDistinctCollection<T>(this BindableCollection<T> collection, Func<T, IComparable> func, params string[] names)
		{
			return new BindableDistinctCollection<T>(collection, (src, dst) =>
			{
				var scomparable = func(src);
				var dcomparable = func(dst);

				if (scomparable != null)
				{
					return scomparable.CompareTo(dcomparable);
				}
				return 0;
			}, names);
		}
	}
}