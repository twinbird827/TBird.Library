using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Wpf.Collections
{
    public class BindableCollection<T> : BindableBase, IBindableCollection<T>
    {
        private IList<T> _list;

        public BindableCollection() : this (Enumerable.Empty<T>())
        {

        }

        public BindableCollection(IEnumerable<T> list)
        {
            _list = new List<T>(list);
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
            set => OnCollectionChanged(_list[index], _list[index] = value, index);
        }

        public int Count => _list.Count;

        public bool IsReadOnly => _list.IsReadOnly;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public virtual void Add(T item)
        {
            _list.Add(item);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, item, Count - 1);
        }

        public virtual void Clear()
        {
            _list.Clear();
            OnCollectionChanged();
        }

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
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
            _list.Insert(index, item);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
        }

        public virtual bool Remove(T item)
        {
            var index = IndexOf(item);
            if (index < 0) return false;

            var result = _list.Remove(item);
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, item, index);
            return result;
        }

        public virtual void RemoveAt(int index)
        {
            if (index < 0 || Count <= index) return;
            Remove(this[index]);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
