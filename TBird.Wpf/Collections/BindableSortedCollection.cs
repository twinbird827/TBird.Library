using System;
using System.Collections.Specialized;
using TBird.Core;

namespace TBird.Wpf.Collections
{
	public class BindableSortedCollection<T> : BindableChildCollection<T>
	{
		private Func<T, T, int> _func;

		internal BindableSortedCollection(BindableCollection<T> collection, Func<T, T, int> func) : base(collection)
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

		public override void Add(T item)
		{
			base.Insert(GetIndex(new FindIndex(0, Count - 1), item), item);
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
			Func<T, T, T> getsrc = isDescending ? (src, dst) => dst : (src, dst) => src;
			Func<T, T, T> getdst = isDescending ? (src, dst) => src : (src, dst) => dst;

			return new BindableSortedCollection<T>(collection, (src, dst) =>
			{
				var scomparable = func(getsrc(src, dst));
				var dcomparable = func(getdst(src, dst));

				if (scomparable != null)
				{
					return scomparable.CompareTo(dcomparable);
				}
				return 0;
			});
		}
	}
}