using TBird.Core;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace TBird.Wpf.Collections
{
	public class BindableWhereCollection<T> : BindableChildCollection<T> where T : IBindable
	{
		private Func<T, bool> _func;

		private HashSet<string> _names;

		internal BindableWhereCollection(BindableCollection<T> collection, Func<T, bool> func, string[] names) : base(collection, false)
		{
			_func = func;
			_names = new HashSet<string>(names);

			collection.ForEach(item =>
			{
				item.AddOnPropertyChanged(this, Item_PropertyChangedEventHandler);
				Add(item);
			});

			AddBindableCollectionChanged((sender, e) =>
			{
				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						e.NewItems.OfType<T>().ForEach(item =>
						{
							item.AddOnPropertyChanged(this, Item_PropertyChangedEventHandler);
							Add(item);
						});
						break;
					case NotifyCollectionChangedAction.Remove:
						e.OldItems.OfType<T>().ForEach(item => Remove(item));
						break;
					case NotifyCollectionChangedAction.Replace:
						e.OldItems.OfType<T>().ForEach(item => Remove(item));
						e.NewItems.OfType<T>().ForEach(Add);
						break;
					case NotifyCollectionChangedAction.Reset:
						Clear();
						break;
					case NotifyCollectionChangedAction.Move:
						throw new NotSupportedException("NotifyCollectionChangedAction is Move.");
				}
			});
		}

		private void Item_PropertyChangedEventHandler(object sender, PropertyChangedEventArgs e)
		{
			if (!_names.Contains(e.PropertyName)) return;

			if (sender is T item)
			{
				if (_func(item) && !Contains(item))
				{
					Add(item);
				}
				else if (!_func(item) && Contains(item))
				{
					Remove(item);
				}
			}
		}

		public override void Add(T item)
		{
			if (_func(item) && !Contains(item))
			{
				if (Parent is IList<T> parent)
				{
					// 親ﾘｽﾄから挿入位置を確認
					var index = parent.Skip(parent.IndexOf(item) + 1)
						.Select(IndexOf)
						.FirstOrDefault(i => 0 <= i, -1);

					// 挿入位置を取得出来たら挿入
					if (0 <= index)
					{
						base.Insert(index, item);
						return;
					}
				}
				base.Add(item);
			}
		}

		public override bool Remove(T item)
		{
			if (Contains(item))
			{
				return base.Remove(item);
			}
			return true;
		}

		public override void AddRange(IEnumerable<T> items)
		{
			items.ForEach(Add);
		}

		public override void Insert(int index, T item)
		{
			Add(item);
		}

		public BindableWhereCollection<T> AddOnRefreshCollection(IBindable bindable, params string[] names)
		{
			bindable.AddOnPropertyChanged(this, (sender, e) =>
			{
				if (!names.Contains(e.PropertyName)) return;

				if (Parent is BindableCollection<T> parent)
				{
					var newitems = new HashSet<T>(parent.Where(x => _func(x)));
					var current = new HashSet<T>(this);
					var delarr = current.Where(x => !newitems.Contains(x)).ToArray();
					var addarr = newitems.Where(x => !current.Contains(x)).ToArray();

					foreach (var item in delarr)
					{
						Remove(item);
					}
					foreach (var item in addarr)
					{
						Add(item);
					}
				}
			});
			return this;
		}
	}

	public static class BindableWhereCollectionExtension
	{
		public static BindableWhereCollection<T> ToBindableWhereCollection<T>(this BindableCollection<T> collection, Func<T, bool> func, params string[] names) where T : IBindable
		{
			return new BindableWhereCollection<T>(collection, func, names);
		}
	}
}