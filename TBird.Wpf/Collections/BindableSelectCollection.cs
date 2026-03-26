using TBird.Core;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace TBird.Wpf.Collections
{
	public class BindableSelectCollection<TSource, TResult> : BindableChildCollection<TResult>
		where TSource : class
		where TResult : class
	{
		internal BindableSelectCollection(BindableCollection<TSource> collection, Func<TSource, TResult> func) : base(collection, true)
		{
			AddRange(collection.Select(func));

			AddBindableCollectionChanged((sender, e) =>
			{
				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						for (var i = 0; i < e.NewItems.Count; i++)
						{
							Insert(e.NewStartingIndex + i, func((TSource)e.NewItems[i]));
						}
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

		internal BindableSelectCollection(BindableCollection<TSource> collection, Func<TSource, Task<TResult>> func) : base(collection, true)
		{
			InitializeCollection(collection, func);

			AddBindableCollectionChanged(async (sender, e) =>
			{
				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						for (var i = 0; i < e.NewItems.Count; i++)
						{
							Insert(e.NewStartingIndex + i, await func((TSource)e.NewItems[i]));
						}
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