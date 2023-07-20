using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace DiscordBot.Music
{
    internal class MusicQueue : IEnumerable, IEnumerable<IMusic>, ICollection, ICollection<IMusic>, IList, IList<IMusic>
    {
        IMusic[] _items;
        int _size;
        int _version;
        int _index;
        [NonSerialized]
        object _syncRoot;
        bool _isFirstTimeDequeue = true;

        static IMusic[] _emptyArray = new IMusic[0];
        static Random random = new Random();

        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot
        {
            get
            {
                if (_syncRoot == null)
                    Interlocked.CompareExchange<object>(ref _syncRoot, new object(), null);
                return _syncRoot;
            }
        }

        public int Count => _size;
        public int CurrentIndex 
        {
            get => _index;
            set => _index = value;
        }
        public bool IsReadOnly => false;
        public bool IsFixedSize => false;
        public IMusic this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_size)
                   throw new ArgumentOutOfRangeException();
                return _items[index];
            }
            set
            {
                if ((uint)index >= (uint)_size)
                    throw new ArgumentOutOfRangeException();
                _items[index] = value;
                _version++;
            }
        }
        public PlayMode PlayMode { get; set; } = new PlayMode();

        object IList.this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_size)
                    throw new ArgumentOutOfRangeException();
                return _items[index];
            }
            set
            {
                if ((uint)index >= (uint)_size)
                    throw new ArgumentOutOfRangeException();
                _items[index] = (IMusic)value;
                _version++;
            }
        }

        public MusicQueue() => _items = _emptyArray;

        public MusicQueue(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new IMusic[capacity];
            _size = 0;
        }

        public MusicQueue(IEnumerable<IMusic> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));
            _items = new IMusic[4];
            _size = 0;
            _version = 0;
            foreach (IMusic item in collection)
                Enqueue(item);
        }

        public void Clear()
        {
            if (_size > 0)
            {
                Array.Clear(_items, 0, _size);
                _size = 0;
            }
            _version++;
        }

        public void CopyTo(IMusic[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            Array.Copy(_items, 0, array, arrayIndex, _size);
        }

        void ICollection<IMusic>.CopyTo(IMusic[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            Array.Copy(_items, 0, array, arrayIndex, _size);
        }

        public void Enqueue(IMusic item) => Add(item);

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<IMusic> IEnumerable<IMusic>.GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        public IMusic Dequeue()
        {
            if (_size == 0)
                throw new InvalidOperationException();
            int index = 0;
            if (PlayMode.isLoopASong)
                index = _index;
            else if (PlayMode.isRandom)
                index = _index = random.Next(0, _size);
            else if (PlayMode.isLoopQueue && !_isFirstTimeDequeue)
            {
                _index++;
                if (_index >= _size)
                    _index = 0;
                index = _index;
            }
            IMusic result = _items[index];
            if (!PlayMode.isLoopQueue && !PlayMode.isLoopASong)
                RemoveAt(index);
            _isFirstTimeDequeue = false;
            return result;
        }

        public IMusic DequeueAt(int index)
        {
            if (_size == 0)
                throw new InvalidOperationException();
            IMusic result = _items[index];
            RemoveAt(index);
            return result;
        }

        public void RandomIndex() => _index = random.Next(0, _size);

        public IMusic Peek()
        {
            if (_size == 0)
                throw new InvalidOperationException();
            return _items[0];
        }

        public bool Contains(IMusic item)
        {
            if (item == null)
            {
                for (int i = 0; i < _size; i++)
                    if (_items[i] == null)
                        return true;
                return false;
            }
            EqualityComparer<IMusic> comparer = EqualityComparer<IMusic>.Default;
            for (int j = 0; j < _size; j++)
            {
                if (comparer.Equals(_items[j], item))
                    return true;
            }
            return false;
        }

        public IMusic[] ToArray()
        {
            IMusic[] array = new IMusic[_size];
            if (_size == 0)
                return array;
            Array.Copy(_items, 0, array, 0, _size);
            return array;
        }

        void SetCapacity(int capacity)
        {
            IMusic[] array = new IMusic[capacity];
            if (_size > 0)
                Array.Copy(_items, array, _items.Length);
            _items = array;
            _version++;
        }

        public void TrimExcess()
        {
            int num = (int)(_items.Length * 0.9);
            if (_size < num)
                SetCapacity(_size);
        }

        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_size)
                throw new ArgumentOutOfRangeException();
            _size--;
            if (index < _size)
                Array.Copy(_items, index + 1, _items, index, _size - index);
            _items[_size] = default;
            _version++;
        }

        public int IndexOf(IMusic item) => Array.IndexOf(_items, item, 0, _size);

        public void Insert(int index, IMusic item)
        {
            if ((uint)index > (uint)_size)
                throw new ArgumentOutOfRangeException();
            if (_size == _items.Length)
                EnsureCapacity(_size + 1);
            if (index < _size)
                Array.Copy(_items, index, _items, index + 1, _size - index);
            _items[index] = item;
            _size++;
            _version++;
        }

        private void EnsureCapacity(int min)
        {
            if (_items.Length < min)
            {
                int num = ((_items.Length == 0) ? 4 : (_items.Length * 2));
                if ((uint)num > 2146435071u)
                    num = 2146435071;
                if (num < min)
                    num = min;
                SetCapacity(num);
            }
        }

        public void Add(IMusic item)
        {
            if (_size == _items.Length)
                EnsureCapacity(_size + 1);
            _items[_size++] = item;
            _version++;
        }

        public bool Remove(IMusic item)
        {
            int num = IndexOf(item);
            if (num >= 0)
            {
                RemoveAt(num);
                return true;
            }
            return false;
        }

        public int Add(object value)
        {
            Add((IMusic)value);
            return Count - 1;
        }

        public bool Contains(object value) => Contains((IMusic)value);

        public int IndexOf(object value) => IndexOf((IMusic)value);

        public void Insert(int index, object value) => Insert(index, (IMusic)value);

        public void Remove(object value) => Remove((IMusic)value);

        public void CopyTo(Array array, int index)
        {
            if (array != null && array.Rank != 1)
                throw new ArgumentException();
            try
            {
                Array.Copy(_items, 0, array, index, _size);
            }
            catch (ArrayTypeMismatchException)
            {
                throw new ArgumentException();
            }
        }

        public struct Enumerator : IEnumerator<IMusic>, IDisposable, IEnumerator
        {
            MusicQueue _q;

            int _index;

            int _version;

            IMusic _currentElement;

            public IMusic Current
            {
                get
                {
                    if (_index < 0)
                        throw new InvalidOperationException();
                    return _currentElement;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (_index < 0)
                        throw new InvalidOperationException();
                    return _currentElement;
                }
            }

            internal Enumerator(MusicQueue q)
            {
                _q = q;
                _version = _q._version;
                _index = -1;
                _currentElement = default;
            }

            public void Dispose()
            {
                _index = -2;
                _currentElement = default;
            }

            public bool MoveNext()
            {
                if (_version != _q._version)
                    throw new InvalidOperationException();
                if (_index == -2)
                    return false;
                _index++;
                if (_index == _q._size)
                {
                    _index = -2;
                    _currentElement = default;
                    return false;
                }
                _currentElement = _q[_index];
                return true;
            }

            void IEnumerator.Reset()
            {
                if (_version != _q._version)
                    throw new InvalidOperationException();
                _index = -1;
                _currentElement = default;
            }
        }
    }
}
