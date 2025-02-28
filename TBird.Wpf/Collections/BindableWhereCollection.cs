using System;
using System.Collections.Specialized;
using System.Linq;
using TBird.Core;

namespace TBird.Wpf.Collections
{
    public class BindableWhereCollection<T> : BindableChildCollection<T>
    {
        private Func<T, bool> _func;

        internal BindableWhereCollection(BindableCollection<T> collection, Func<T, bool> func) : base(collection)
        {
            _func = func;

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

        public override void Add(T item)
        {
            if (_func(item)) base.Add(item);
        }
    }

    public static class BindableWhereCollectionExtension
    {
        public static BindableWhereCollection<T> ToBindableWhereCollection<T>(this BindableCollection<T> collection, Func<T, bool> func)
        {
            return new BindableWhereCollection<T>(collection, func);
        }
    }
}