﻿using ControlzEx.Standard;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Wpf.Collections
{
    public class BindableConvertCollection<TSource, TResult> : BindableChildCollection<TResult>
        where TSource : class
        where TResult : class
    {
        internal BindableConvertCollection(BindableCollection<TSource> collection, Func<TSource, TResult> func) : base(collection)
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

        internal BindableConvertCollection(BindableCollection<TSource> collection, Func<TSource, Task<TResult>> func) : base(collection)
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
