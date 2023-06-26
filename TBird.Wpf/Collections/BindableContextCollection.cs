using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Wpf.Collections
{
    public class BindableContextCollection<T> : BindableChildCollection<T>
    {
        private SynchronizationContext _context;

        internal BindableContextCollection(BindableCollection<T> collection, SynchronizationContext context) : base(collection)
        {
            _context = context;
            collection.ForEach(item => Add(item));

            AddCollectionChanged(collection, (sender, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        Insert(e.NewStartingIndex, (T)e.NewItems[0]);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        RemoveAt(e.OldStartingIndex);
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

        public override T this[int index]
        {
            get => base[index];
            set => Post(
                args => base[(int)args[0]] = (T)args[1],
                index, value
            );
        }
        public override void Add(T item)
        {
            Post(args => base.Add((T)args[0]), item);
        }

        public override void Clear()
        {
            Post(_ => base.Clear(), null);
        }

        public override int IndexOf(T item)
        {
            return base.IndexOf(item);
        }

        public override void Insert(int index, T item)
        {
            Post(
                args => base.Insert((int)args[0], (T)args[1]),
                index, item
            );
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

        private void Post(Action<object[]> post, params object[] args)
        {
            _context.Post(x => post((object[])x), args);
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
