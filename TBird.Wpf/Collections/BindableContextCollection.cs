using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TBird.Wpf.Collections
{
    public class BindableContextCollection<T> : BindableCollection<T>
    {
        private SynchronizationContext _context;

        internal BindableContextCollection(BindableCollection<T> collection, SynchronizationContext context)
        {
            _context = context;

            foreach (var item in collection)
            {
                Add(item);
            }

            AddCollectionChanged(collection, (sender, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        Insert(e.NewStartingIndex, (T)e.NewItems[0]);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        RemoveAt(e.NewStartingIndex);
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

            AddDisposed((sender, e) =>
            {
                _context = null;
            });
        }

        public override T this[int index]
        {
            get => base[index]; 
            set => _context.Post(args =>
            {
                var argsarr = (object[])args;
                var argsindex = (int)argsarr[0];
                var argsvalue = (T)argsarr[1];

                base[argsindex] = argsvalue;
            }, new object[] { index, value }); 
        }
        public override void Add(T item)
        {
            _context.Post(x => base.Add((T)x), item);
        }

        public override void Clear()
        {
            _context.Post(_ => base.Clear(), null);
        }

        public override int IndexOf(T item)
        {
            return base.IndexOf(item);
        }

        public override void Insert(int index, T item)
        {
            _context.Post(args =>
            {
                var argsarr = (object[])args;
                var argsindex = (int)argsarr[0];
                var argsitem = (T)argsarr[1];
                base.Insert(argsindex, argsitem);
            }, new object[] { index, item });
        }

        public override bool Remove(T item)
        {
            if (IndexOf(item) < 0)
            {
                return false;
            }
            else
            {
                _context.Post(x => base.Remove((T)x), item);
                return true;
            }
        }

        public override void RemoveAt(int index)
        {
            _context.Post(x => base.RemoveAt((int)x), index);
        }
    }

    public static class BindableContextCollectionExtension
    {
        public static BindableContextCollection<T> ToBindableContextCollection<T>(this BindableCollection<T> collection, SynchronizationContext context)
        {
            return new BindableContextCollection<T>(collection, context);
        }

        public static BindableContextCollection<T> ToBindableContextCollection<T>(this BindableCollection<T> collection)
        {
            return ToBindableContextCollection(collection, WpfUtil.GetContext());
        }
    }
}
