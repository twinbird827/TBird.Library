using TBird.Core;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace TBird.Wpf.Collections
{
	public class BindableSortedCollection<T> : BindableChildCollection<T>
	{
		private Func<T, T, int> _func;

		internal BindableSortedCollection(BindableCollection<T> collection, Func<T, T, int> func) : base(collection, false)
		{
			_func = func;

			AddRange(collection);

			AddBindableCollectionChanged((sender, e) =>
			{
				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						for (var i = 0; i < e.NewItems.Count; i++)
						{
							Add((T)e.NewItems[i]);
						}
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
			base.Insert(GetIndex(new FindIndex(0, Count - 1), item), item);
		}

		public override void AddRange(IEnumerable<T> items)
		{
			items.ForEach(item => Add(item));
		}

		public override void Insert(int index, T item)
		{
			throw new NotSupportedException(nameof(Insert));
		}

		public override void RemoveAt(int index)
		{
			throw new NotSupportedException(nameof(RemoveAt));
		}

		private int GetIndex(FindIndex find, T item)
		{
			if (find.Count == 1)
			{
				return _func(item, this[find.Start]) < 0 ? 0 : 1;
			}

			if (find.Start == find.End || find.End < 0) return find.Start;

			if (find.Count == 2)
			{
				return _func(item, this[find.Start]) < 0
					? find.Start
					: _func(item, this[find.End]) < 0
					? find.End
					: find.End + 1;
			}

			var center = _func(item, this[find.Center]);
			if (center < 0)
			{
				return GetIndex(new FindIndex(find.Start, find.Center), item);
			}
			else
			{
				return GetIndex(new FindIndex(find.Center, find.End), item);
			}
		}

		private struct FindIndex
		{
			public int Start;
			public int End;
			public int Center => (End - Start) / 2 + Start;
			public int Count => End - Start + 1;

			public FindIndex(int start, int end)
			{
				Start = start;
				End = end;
			}
		}
	}

	public static class BinbdaleSortedCollectionExtension
	{
		public static BindableSortedCollection<T> ToBindableSortedCollection<T>(this BindableCollection<T> collection, Func<T, T, int> func)
		{
			return new BindableSortedCollection<T>(collection, func);
		}

		public static BindableSortedCollection<T> ToBindableSortedCollection<T>(this BindableCollection<T> collection, Func<T, IComparable> func, bool isDescending)
		{
			return new BindableSortedCollection<T>(collection, (src, dst) =>
			{
				var scomparable = func(isDescending ? dst : src);
				var dcomparable = func(isDescending ? src : dst);

				if (scomparable != null)
				{
					return scomparable.CompareTo(dcomparable);
				}
				return 0;
			});
		}
	}
}