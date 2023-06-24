using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Wpf.Collections
{
    public class BindableSortedCollection<T> : BindableCollection<T>
    {
        private Func<T, T, bool> _func;

        internal BindableSortedCollection(BindableCollection<T> collection, Func<T, T, bool> func)
        {
            _func = func;

            AddCollectionChanged(collection, (sender, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        Add((T)e.NewItems[0]);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        Remove((T)e.NewItems[0]);
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
            for (var i = 0; i < Count; i++)
            {
                if (_func(this[i], item))
                {
                    base.Insert(i, item);
                    return;
                }
            }
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
    }

    public static class BinbdaleSortedCollectionExtension
    {
        public static BindableSortedCollection<T> ToBindableSortedCollection<T>(this BindableCollection<T> collection, Func<T, T, bool> func)
        {
            return new BindableSortedCollection<T>(collection, func);
        }

        public static BindableSortedCollection<T> ToBindableSortedCollection<T>(this BindableCollection<T> collection, Func<T, IComparable> func, bool isDescending)
        {
            return new BindableSortedCollection<T>(collection, (src, dst) =>
            {
                var scomparable = func(src);
                var dcomparable = func(dst);

                if (scomparable != null && dcomparable != null)
                {
                    return 0 <= (isDescending ? scomparable.CompareTo(dcomparable) : dcomparable.CompareTo(scomparable));
                }
                else if (scomparable != null)
                {
                    return isDescending;
                }
                else if (dcomparable != null)
                {
                    return !isDescending;
                }
                else
                {
                    return true;
                }
            });
        }
    }
}
