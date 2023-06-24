using ControlzEx.Standard;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Wpf.Collections
{
    public class BindableConvertCollection<TSource, TResult> : BindableCollection<TResult>
        where TSource : class
        where TResult : class
    {
        internal BindableConvertCollection(BindableCollection<TSource> collection, Func<TSource, TResult> func)
        {
            foreach (var item in collection)
            {
                Add(func(item));
            }

            AddCollectionChanged(collection, (sender, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        Insert(e.NewStartingIndex, func((TSource)e.NewItems[0]));
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        RemoveAt(e.NewStartingIndex);
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
    }

    public static class BindableConvertCollectionExtension
    {
        public static BindableConvertCollection<TSource, TResult> ToBindableConvertCollection<TSource, TResult>(this BindableCollection<TSource> collection, Func<TSource, TResult> func)
            where TSource : class
            where TResult : class
        {
            return new BindableConvertCollection<TSource, TResult>(collection, func);
        }
    }
}
