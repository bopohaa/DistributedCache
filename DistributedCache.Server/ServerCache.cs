using DistributedCache.Common;
using DistributedCache.Common.Internal;
using CoreCommon;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedCache
{
    public class ServerCache : IServerCache
    {
        private readonly TimeSpan _expireTimeChunk;
        private DateTime _expireCacheChunk;
        private Dictionary<byte[], CacheItem> _current;
        private Dictionary<byte[], CacheItem> _previous;
        private readonly CapedArray<byte[]> _expiredKeys;
        private readonly ReaderWriterLockSlim _lock;

        public event Action<ICollection<CacheValue>> OnItems;

        public ServerCache(TimeSpan expire_time_chunk, int max_expired_keys)
        {
            _expireTimeChunk = expire_time_chunk;
            _expireCacheChunk = DateTime.UtcNow + _expireTimeChunk;
            _current = new Dictionary<byte[], CacheItem>(new ByteArrayComparer());
            _previous = new Dictionary<byte[], CacheItem>(new ByteArrayComparer());
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _expiredKeys = new CapedArray<byte[]>(max_expired_keys);
        }

        public ICollection<CacheValue> TryGetItems(IEnumerable<byte[]> keys)
        {
            _lock.EnterReadLock();
            try
            {
                var now = DateTime.UtcNow;
                var res = new List<CacheValue>();
                CacheItem val;
                foreach (var key in keys)
                {
                    bool expired = _current.TryGetValue(key, out val) || _previous.TryGetValue(key, out val) ?
                        val.Expired < now : true;
                    if (expired)
                        _expiredKeys.Push(key);
                    else
                        res.Add(new CacheValue(key, val.Payload, val.Expired - now));
                }

                return res;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public ICollection<byte[]> PutItems(ICollection<CacheValue> items)
        {
            _lock.EnterWriteLock();
            try
            {
                var now = DateTime.UtcNow;
                if (_expireCacheChunk < now)
                {
                    var curr = _previous;
                    _previous = _current;
                    _current = curr;
                    _current.Clear();
                    _expireCacheChunk = now + _expireTimeChunk;
                }

                var expiredKeys = new HashSet<byte[]>(_expiredKeys.Reset(), new ByteArrayComparer());

                if (items.Count > 0)
                {
                    foreach (var item in items)
                    {
                        _current[item.Key] = new CacheItem() { Expired = now.AddSeconds(item.ExpiredAtSeconds), Payload = item.Value };
                        expiredKeys.Remove(item.Key);
                    }

                    OnItems.Invoke(items);
                }

                return expiredKeys;
            }
            finally { _lock.ExitWriteLock(); }

        }
    }

    internal class CapedArray<T> where T : class
    {
        private int _idx;
        private T[] _data;
        private readonly ReaderWriterLockSlim _lock;
        private int _count;

        public CapedArray(int capacity)
        {
            _idx = -1;
            _count = 0;
            _data = new T[capacity];
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        public void Push(T value)
        {
            _lock.EnterReadLock();
            try
            {
                unchecked
                {
                    var data = Volatile.Read(ref _data);
                    var idx = ((uint)Interlocked.Increment(ref _idx)) % _data.Length;
                    Interlocked.Increment(ref _count);
                    Volatile.Write(ref data[idx], value);
                }
            }
            finally { _lock.ExitReadLock(); }
        }

        public ArraySegment<T> Reset()
        {
            _lock.EnterWriteLock();
            try
            {
                var data = Volatile.Read(ref _data);
                var count = Math.Min(_count, data.Length);

                Volatile.Write(ref _data, new T[data.Length]);
                Volatile.Write(ref _idx, -1);
                Volatile.Write(ref _count, 0);

                return new ArraySegment<T>(data, 0, count);
            }
            finally { _lock.ExitWriteLock(); }
        }
    }
}
