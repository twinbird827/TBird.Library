using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
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

            collection.ForEach(item => Add(item));

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
            if (this.Any(x => _func(x, item) == 0)) return;
            if (item is IBindable bindable) AddOnRefreshCollection(bindable, _names);
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

        public BindableDistinctCollection<T> AddOnRefreshCollection(IBindable bindable, params string[] names)
        {
            bindable.AddOnPropertyChanged(this, (sender, e) =>
            {
                if (!names.Contains(e.PropertyName)) return;

                if (Parent is BindableCollection<T> parent)
                {
                    // 重複を改めて削除
                    for (var i = Count - 1; 0 <= i; i--)
                    {
                        var item = this[i];
                        if (this.Take(i).Any(x => _func(x, item) == 0))
                        {
                            Remove(item);
                        }
                    }

                    // 親から追加
                    parent.ForEach(x => Add(x));
                }
            });
            return this;
        }

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