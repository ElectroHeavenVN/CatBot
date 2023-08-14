using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DiscordBot.Music
{
    internal class MusicQueue : IEnumerable, IEnumerable<IMusic>, ICollection, ICollection<IMusic>, IList, IList<IMusic>
    {
        List<IMusic> _items;
        Random random = new Random();

        public MusicQueue() => _items = new List<IMusic>();
        public MusicQueue(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new List<IMusic>(capacity);
        }

        public MusicQueue(IEnumerable<IMusic> collection) : this()
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));
            foreach (IMusic item in collection)
                Enqueue(item);
        }

        public IMusic this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }
        object IList.this[int index] 
        {
            get => this[index];
            set => this[index] = (IMusic)value;
        }

        public IEnumerator GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        IEnumerator<IMusic> IEnumerable<IMusic>.GetEnumerator() => _items.GetEnumerator();

        public int Count => _items.Count;
        public bool IsReadOnly => false;
        public bool IsSynchronized => false;
        public object SyncRoot => typeof(List<IMusic>).GetProperty("SyncRoot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_items);
        public bool IsFixedSize => false;

        public void Add(IMusic item) => _items.Add(item);
        public int Add(object value)
        {
            Add((IMusic)value);
            return _items.Count - 1;
        }
        public void AddRange(IEnumerable<IMusic> collection) => _items.AddRange(collection);

        public void Clear() => _items.Clear();

        public bool Contains(IMusic item) => _items.Contains(item);
        public bool Contains(object value) => Contains((IMusic)value);

        public void CopyTo(IMusic[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        void ICollection<IMusic>.CopyTo(IMusic[] array, int arrayIndex) => CopyTo(array, arrayIndex);
        public void CopyTo(Array array, int index) => CopyTo((IMusic[])array, index);

        public IMusic Dequeue()
        {
            IMusic result = this[0];
            RemoveAt(0);
            return result;
        }
        public IMusic DequeueAt(int index)
        {
            IMusic result = this[index];
            RemoveAt(index);
            return result;
        }
        public void Enqueue(IMusic item) => Add(item);

        public int IndexOf(IMusic item) => _items.IndexOf(item);
        public int IndexOf(object value) => IndexOf((IMusic)value);

        public void Insert(int index, IMusic item) => _items.Insert(index, item);
        public void Insert(int index, object value) => Insert(index, (IMusic)value);
        public void InsertRange(int index, IEnumerable<IMusic> collection) => _items.InsertRange(index, collection);

        public IMusic Peek() => _items[0];

        public IMusic Pop()
        {
            IMusic result = this.Last();
            Remove(result);
            return result;
        }
        public IMusic PopAt(int index)
        {
            IMusic result = this[index];
            RemoveAt(index);
            return result;
        }
        public void Push(IMusic item) => Enqueue(item);

        public void RemoveAt(int index) => _items.RemoveAt(index);
        public bool Remove(IMusic item) => _items.Remove(item);
        public void Remove(object value) => Remove((IMusic)value);

        public void Shuffle()
        {
            List<IMusic> newQueue = new List<IMusic>();
            for (int i = 0; i < Count; i++)
                newQueue.Add(DequeueAt(random.Next(0, Count)));
            _items = newQueue;
        }

        public IMusic[] ToArray() => _items.ToArray();
    }
}
