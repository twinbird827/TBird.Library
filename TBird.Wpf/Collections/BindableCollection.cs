using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using TBird.Core;

namespace TBird.Wpf.Collections
{
    public abstract class BindableCollection : BindableBase
    {
        internal object LockObject { get; set; }
    }

    public class BindableCollection<T> : BindableCollection, IBindableCollection<T>
    {
        private IList<T> _list;

        public BindableCollection() : this(Enumerable.Empty<T>())
        {

        }

        public BindableCollection(IEnumerable<T> enumerable, bool disposesource = false)
        {
            LockObject = Guid.NewGuid().ToString();
            _list = new List<T>(enumerable);

            AddDisposed((sender, e) =>
            {
                _list.Clear();

                if (disposesource)
                {
                    enumerable.ForEach(x => x.TryDispose());
                    if (enumerable is IList list) list.Clear();
                }
            });
        }

        private void OnCollectionChanged(bool isnotifycount, bool isnotifyitem, NotifyCollectionChangedEventArgs e)
        {
            if (isnotifycount) OnPropertyChanged(nameof(Count));
            if (isnotifyitem) OnPropertyChanged("Item[]");
            CollectionChanged?.Invoke(this, e);
        }

        private void OnCollectionChanged()
        {
            OnCollectionChanged(false, false, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, int index)
        {
            OnCollectionChanged(true, true, new NotifyCollectionChangedEventArgs(action, item, index));
        }

        private void OnCollectionChanged(object oldi, object newi, int index)
        {
            OnCollectionChanged(false, true, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, oldi, newi, index));
        }

        public virtual T this[int index]
        {
            get => _list[index];
            set
            {
                lock (LockObject)
                {
                    OnCollectionChanged(_list[index], _list[index] = value, index);
                }
            }
        }

        public int Count => _list.Count;

        public bool IsReadOnly => _list.IsReadOnly;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public virtual void Add(T item)
        {
            lock (LockObject)
            {
                _list.Add(item);
                OnCollectionChanged(NotifyCollectionChangedAction.Add, item, Count - 1);
            }
        }

        public virtual void Clear()
        {
            lock (LockObject)
            {
                _list.Clear();
                OnCollectionChanged();
            }
        }

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (LockObject)
            {
                _list.CopyTo(array, arrayIndex);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public virtual int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        public virtual void Insert(int index, T item)
        {
            lock (LockObject)
            {
                _list.Insert(index, item);
                OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
            }
        }

        public virtual bool Remove(T item)
        {
            var result = false;
            lock (LockObject)
            {
                var index = IndexOf(item);
                if (index < 0) return false;

                result = _list.Remove(item);
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, item, index);
            }
            return result;
        }

        public virtual void RemoveAt(int index)
        {
            lock (LockObject)
            {
                if (index < 0 || Count <= index) return;
                Remove(this[index]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}