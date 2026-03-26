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

		private string[] _names;

		internal BindableWhereCollection(BindableCollection<T> collection, Func<T, bool> func, string[] names) : base(collection, false)
		{
			_func = func;
			_names = names;

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
						this[e.NewStartingIndex] = (T)e.NewItems[0];
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
					for (var i = parent.IndexOf(item) + 1; i < parent.Count; i++)
					{
						var index = IndexOf(parent[i]);
						if (0 <= index)
						{
							base.Insert(index, item);
							return;
						}
					}

				}
				base.Add(item);
			}
		}

		public override bool Remove(T item)
		{
			if (!_func(item) && Contains(item))
			{
				return base.Remove(item);
			}
			return true;
		}

		public override void AddRange(IEnumerable<T> items)
		{
			items.ForEach(item => Add(item));
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
					var newitems = parent.Where(x => _func(x)).ToArray();
					var delarr = this.Where(x => !newitems.Contains(x)).ToArray();
					var addarr = newitems.Where(x => !Contains(x)).ToArray();

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