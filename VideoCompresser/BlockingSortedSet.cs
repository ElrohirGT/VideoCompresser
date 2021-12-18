using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace VideoCompresser
{
    public class BlockingSortedSet<T> : IProducerConsumerCollection<T>
    {
        private readonly object _lock = new();
        private readonly SortedSet<T> _set;

        public BlockingSortedSet() => _set = new SortedSet<T>();

        public BlockingSortedSet(IComparer<T>? comparer) => _set = new SortedSet<T>(comparer);

        public BlockingSortedSet(IEnumerable<T> collection) => _set = new SortedSet<T>(collection);

        public BlockingSortedSet(IEnumerable<T> collection, IComparer<T>? comparer) => _set = new SortedSet<T>(collection, comparer);

        public int Count => _set.Count;
        public bool IsSynchronized => true;
        public object SyncRoot => _lock;

        public void CopyTo(T[] array, int index)
        {
            lock (_lock)
                ((ICollection)_set).CopyTo(array, index);
        }

        public void CopyTo(Array array, int index)
        {
            lock (_lock)
                ((ICollection)_set).CopyTo(array, index);
        }

        public IEnumerator<T> GetEnumerator() => _set.GetEnumerator();

        public T[] ToArray() => _set.ToArray();

        public bool TryAdd(T item)
        {
            lock (_lock)
                return _set.Add(item);
        }

        public bool TryTake([MaybeNullWhen(false)] out T item)
        {
            lock (_lock)
            {
                item = _set.Min ?? default;
                return _set.Remove(item);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}