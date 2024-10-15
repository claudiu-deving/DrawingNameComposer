using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DrawingNameComposer
{
	/// <summary>
	/// Observable Collection that enables the usage of AddRange and RemoveRange.
	/// It stops the notification until the EndUpdate is called.
	/// </summary>
	/// <typeparam name="T">The type of item in collection</typeparam>
	public class ExtendedObservableCollection<T> : ObservableCollection<T>
	{
		public ExtendedObservableCollection() : base() { }
		public ExtendedObservableCollection(IEnumerable<T> values) : base(values) { }

		/// <summary>
		/// Uses the default comparer to check if the item added is a new one.
		/// </summary>
		/// <param name="items"></param>
		public void AddRangeIfNew(IEnumerable<T> items)
		{
			if (items == null) return;

			BeginUpdate();
			foreach (T item in items)
			{
				AddIfNew(item);
			}
			EndUpdate();
		}

		private void AddIfNew(T item)
		{
			if (!Contains(item))
			{
				Add(item);
			}
		}

		private bool _suppressNotification = false;

		private void BeginUpdate()
		{
			_suppressNotification = true;
		}

		private void EndUpdate()
		{
			_suppressNotification = false;
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			if (!_suppressNotification)
			{
				base.OnCollectionChanged(e);
			}
		}

		/// <summary>
		/// Removes a range of items from collection without triggering notifications for each item.
		/// </summary>
		/// <param name="toBeRemoved"></param>
		public void RemoveRange(List<T> toBeRemoved)
		{
			BeginUpdate();
			foreach (var item in from item in toBeRemoved
								 where Contains(item)
								 select item)
			{
				Remove(item);
			}

			EndUpdate();
		}

		/// <summary>
		/// Adds a range of items to collection without triggering notifications for each item.
		/// </summary>
		/// <param name="toBeAdded"></param>
		public void AddRange(IEnumerable<T> toBeAdded)
		{
			BeginUpdate();
			foreach (var item in from item in toBeAdded
								 where !Contains(item)
								 select item)
			{
				Add(item);
			}

			EndUpdate();
		}


		/// <summary>
		/// Add or insert an item at a specific index if it is not already in the collection.
		/// </summary>
		/// <param name="newIndex"></param>
		/// <param name="item"></param>
		public void AddOrInsertIfNew(int newIndex, T item)
		{
			if (Contains(item))
			{
				return;
			}
			if (newIndex >= Count)
			{
				Add(item);
			}
			else if (newIndex < 0)
			{
				Insert(0, item);
			}
			else
			{
				Insert(newIndex, item);
			}
		}

		/// <summary>
		/// Adds a range of items to collection without triggering notifications for each item.
		/// </summary>
		/// <param name="itemsToInsert"></param>
		public void AddOrInsertRangeIfNew(List<Tuple<int, T>> itemsToInsert)
		{
			BeginUpdate();
			foreach (var item in itemsToInsert)
			{
				AddOrInsertIfNew(item.Item1, item.Item2);
			}
			EndUpdate();
		}
	}
}