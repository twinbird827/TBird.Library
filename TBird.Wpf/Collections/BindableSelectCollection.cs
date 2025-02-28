using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Wpf.Collections
{
    public class BindableSelectCollection<TSource, TResult> : BindableChildCollection<TResult>
        where TSource : class
        where TResult : class
    {
        internal BindableSelectCollection(BindableCollection<TSource> collection, Func<TSource, TResult> func) : base(collection)
        {
            collection.ForEach(x => Add(func(x)));

            AddCollectionChanged(collection, (sender, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        Insert(e.NewStartingIndex, func((TSource)e.NewItems[0]));
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        RemoveAt(e.OldStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        this[e.NewStartingIndex] = func((TSource)e.NewItems[0]);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        Clear();
                        break;
                    case NotifyCollectionChangedAction.Move:
                        throw new NotSupportedException("NotifyCollectionChangedAction is Move.");
                }
            });
        }

        internal BindableSelectCollection(BindableCollection<TSource> collection, Func<TSource, Task<TResult>> func) : base(collection)
        {
            InitializeCollection(collection, func);

            AddCollectionChanged(collection, async (sender, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        Insert(e.NewStartingIndex, await func((TSource)e.NewItems[0]));
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        RemoveAt(e.OldStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        this[e.NewStartingIndex] = await func((TSource)e.NewItems[0]);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        Clear();
                        break;
                    case NotifyCollectionChangedAction.Move:
                        throw new NotSupportedException("NotifyCollectionChangedAction is Move.");
                }
            });
        }

        private async void InitializeCollection(BindableCollection<TSource> collection, Func<TSource, Task<TResult>> func)
        {
            foreach (var item in collection)
            {
                Add(await func(item));
            }
        }
    }

    public static class BindableSelectCollectionExtension
    {
        public static BindableSelectCollection<TSource, TResult> ToBindableSelectCollection<TSource, TResult>(this BindableCollection<TSource> collection, Func<TSource, TResult> func)
            where TSource : class
            where TResult : class
        {
            return new BindableSelectCollection<TSource, TResult>(collection, func);
        }
    }
}