using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using TBird.Core;

namespace TBird.Wpf.Collections
{
	public class BindableContextCollection<T> : BindableChildCollection<T>, INotifyCollectionChanged
	{
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		protected override void OnCollectionChanged(bool isnotifycount, bool isnotifyitem, NotifyCollectionChangedEventArgs e)
		{
			if (IsDisposed) return;
			base.OnCollectionChanged(isnotifycount, isnotifyitem, e);
			if (CollectionChanged != null) CollectionChanged(this, e);
		}

		private SynchronizationContext _context;

		private int _chunk;

		internal BindableContextCollection(BindableCollection<T> collection, SynchronizationContext context, int chunk) : base(collection, false)
		{
			_chunk = chunk;
			_context = context;
			AddRange(collection);

			AddBindableCollectionChanged((sender, e) =>
			{
				void MultiPost(int index, IList items, Action<int, T> action)
				{
					items.OfType<T>().Chunk(_chunk).ForEach(arr =>
					{
						Post(args =>
						{
							var newindex = (int)args[0];
							var newitems = (T[])args[1];
							for (var i = 0; i < newitems.Length; i++)
							{
								action(newindex + i, newitems[i]);
							}
						}, index, arr.ToArray());
						index += _chunk;
					});
				}

				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						MultiPost(e.NewStartingIndex, e.NewItems, (i, item) => base.Insert(i, item));
						break;
					case NotifyCollectionChangedAction.Remove:
						MultiPost(0, e.OldItems, (i, item) => Remove(item));
						break;
					case NotifyCollectionChangedAction.Replace:
						MultiPost(e.NewStartingIndex, e.NewItems, (i, item) => base[i] = item);
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

		public override void AddRange(IEnumerable<T> items)
		{
			Post(args => base.AddRange((IEnumerable<T>)args[0]), items);
		}

		public override void Clear()
		{
			Post(_ => base.Clear());
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
				Post(x => base.Remove(x), item);
				return true;
			}
		}

		public override void RemoveAt(int index)
		{
			Post(x => base.RemoveAt(x), index);
		}

		private void Post(Action<object[]> post, params object[] args)
		{
			_context.Post(x => post((object[])x), args);
		}

		private void Post<TItem>(Action<TItem> post, TItem args)
		{
			_context.Post(x => post((TItem)x), args);
		}

	}

	public static class BindableContextCollectionExtension
	{
		public static BindableContextCollection<T> ToBindableContextCollection<T>(this BindableCollection<T> collection, SynchronizationContext context, int chunk = 10)
		{
			return new BindableContextCollection<T>(collection, context, chunk);
		}

		public static BindableContextCollection<T> ToBindableContextCollection<T>(this BindableCollection<T> collection, int chunk = 10)
		{
			return ToBindableContextCollection(collection, WpfUtil.GetContext(), chunk);
		}
	}
}