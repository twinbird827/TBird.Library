using TBird.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace TBird.Wpf.Collections
{
	public abstract class BindableCollection : BindableBase
	{
		internal object LockObject { get; set; }

		internal event NotifyCollectionChangedEventHandler BindableCollectionChanged;

		protected virtual void OnCollectionChanged(bool isnotifycount, bool isnotifyitem, NotifyCollectionChangedEventArgs e)
		{
			if (IsDisposed) return;
			if (isnotifycount) OnPropertyChanged("Count");
			if (isnotifyitem) OnPropertyChanged("Item[]");
			if (BindableCollectionChanged != null) BindableCollectionChanged(this, e);
		}
	}

	public class BindableCollection<T> : BindableCollection, IBindableCollection<T>
	{
		protected IList<T> _list;
		protected bool _disposedsource;

		public BindableCollection(bool disposesource = true) : this(Enumerable.Empty<T>(), disposesource)
		{

		}

		public BindableCollection(IEnumerable<T> enumerable, bool disposesource = true)
		{
			LockObject = Guid;
			_list = new List<T>(enumerable);
			_disposedsource = disposesource;

			AddDisposed((sender, e) =>
			{
				var arr = _list.ToArray();
				_list.Clear();
				if (_disposedsource) arr.ForParallel(x => x.TryDispose());
			});
		}

		private void OnCollectionChanged(bool changed)
		{
			OnCollectionChanged(changed, changed, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, int index)
		{
			OnCollectionChanged(true, true, new NotifyCollectionChangedEventArgs(action, item, index));
		}

		private void OnCollectionChanged(object oldi, object newi, int index)
		{
			OnCollectionChanged(false, true, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newi, oldi, index));
		}

		protected virtual void Action(Action action)
		{
			if (IsDisposed) return;
			lock (LockObject)
			{
				if (!IsDisposed)
				{
					action();
				}
			}
		}

		protected virtual TResult Action<TResult>(Func<TResult> action)
		{
			if (IsDisposed) return default;
			lock (LockObject)
			{
				if (!IsDisposed)
				{
					return action();
				}
				return default;
			}
		}

		public virtual T this[int index]
		{
			get => Action(() => _list[index]);
			set
			{
				T oldi = default(T);
				T newi = default(T);
				Action(() =>
				{
					oldi = _list[index];
					newi = _list[index] = value;
				});
				OnCollectionChanged(oldi, newi, index);
			}
		}

		public int Count { get { lock (LockObject) { return _list.Count; } } }

		public bool IsReadOnly => _list.IsReadOnly;

		public virtual void Add(T item)
		{
			var count = 0;

			Action(() =>
			{
				_list.Add(item);
				count = _list.Count - 1;
			});
			OnCollectionChanged(NotifyCollectionChangedAction.Add, item, count);
		}

		public virtual void AddRange(IEnumerable<T> items)
		{
			var itemlist = items.ToList();
			var count = 0;

			Action(() =>
			{
				_list.AddRange(itemlist);
				count = _list.Count - itemlist.Count;
			});
			OnCollectionChanged(true, true, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemlist, count));
		}

		public virtual void Clear()
		{
			T[] arr = default;
			Action(() =>
			{
				arr = _list.ToArray();
				_list.Clear();
			});
			if (arr != null) OnCollectionChanged(arr.Length > 0);
			if (_disposedsource) WpfUtil.Post(() => arr.ForParallel(x => x.TryDispose()));
		}

		public bool Contains(T item)
		{
			return Action(() => _list.Contains(item));
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			Action(() =>
			{
				_list.CopyTo(array, arrayIndex);
			});
		}

		public IEnumerator<T> GetEnumerator()
		{
			IEnumerable<T> snapshot = Enumerable.Empty<T>();
			Action(() =>
			{
				snapshot = _list.ToArray();
			});
			return snapshot.GetEnumerator();
		}

		public virtual int IndexOf(T item)
		{
			return Action(() => _list.IndexOf(item));
		}

		public virtual void Insert(int index, T item)
		{
			Action(() =>
			{
				_list.Insert(index, item);
			});
			OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
		}

		public virtual bool Remove(T item)
		{
			var index = -1;
			var action = Action(() =>
			{
				index = _list.IndexOf(item);
				if (index < 0) return false;
				return _list.Remove(item);
			});
			if (action)
			{
				OnCollectionChanged(NotifyCollectionChangedAction.Remove, item, index);
				if (_disposedsource) WpfUtil.Post(val => val.TryDispose(), item);
			}
			return action;
		}

		public virtual void RemoveAt(int index)
		{
			T item = default;
			var action = Action(() =>
			{
				if (index < 0 || _list.Count <= index) return false;
				item = _list[index];
				_list.RemoveAt(index);
				return true;
			});
			if (action)
			{
				OnCollectionChanged(NotifyCollectionChangedAction.Remove, item, index);
				if (_disposedsource) WpfUtil.Post(val => val.TryDispose(), item);
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}