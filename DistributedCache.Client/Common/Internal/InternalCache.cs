using CoreCommon;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DistributedCache.Common.Internal
{
    internal class InternalCache<Tk, T>: ICache<Tk, T>
    {
        private readonly Dictionary<Tk, CacheItem<T>>[] _cache;
        private readonly DateTime[] _cacheExpire;
        private readonly ReaderWriterLockSlim _lock;
        private readonly int _chunkCount;
        private int _cacheIdx;

        public InternalCache(int chunk_count, IEqualityComparer<Tk> comparer)
        {
            _cache = new Dictionary<Tk, CacheItem<T>>[chunk_count];
            _cacheExpire = new DateTime[chunk_count];
            _chunkCount = chunk_count;
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _cacheIdx = 0;
            for (var i = 0; i < chunk_count; i++)
            {
                _cache[i] = new Dictionary<Tk, CacheItem<T>>(comparer);
                _cacheExpire[i] = DateTime.MinValue;
            }
        }

        public bool TryGet(Tk key, out T value, out bool expired)
        {
            _lock.EnterReadLock();
            try
            {
                var now = DateTime.UtcNow;
                var idx = Volatile.Read(ref _cacheIdx);
                for (var i = 0; i < _chunkCount; i++)
                {
                    var n = (idx + i) % _chunkCount;
                    //if (now > _cacheExpire[n])
                    //    continue;
                    var cache = _cache[n];
                    if (cache.TryGetValue(key, out var item))
                    {

                        expired = item.Expired < now;
                        value = item.Payload;
                        return true;

                        //cache.Remove(key);
                    }
                }
                expired = false;
                value = default(T);
                return false;
            }
            finally { _lock.ExitReadLock(); }
        }

        public void AddRange(CacheValue<Tk, T>[] items)
        {
            _lock.EnterWriteLock();
            try
            {
                var now = DateTime.UtcNow;
                var idx = Volatile.Read(ref _cacheIdx);
                var cache = _cache[idx];
                var maxExpire = DateTime.MinValue;
                var end = items.Length;
                for (var i = 0; i < end; i++)
                {
                    var expired = now.AddSeconds(items[i].ExpiredAtSeconds);
                    var key = items[i].Key;

                    for (var j = 0; j < _chunkCount; j++)
                        if (_cache[j].Remove(key)) break;

                    cache.Add(key, new CacheItem<T>() { Expired = expired, Payload = items[i].Value });

                    if (maxExpire < expired)
                        maxExpire = expired;
                }
                if (_cacheExpire[idx] < maxExpire)
                    _cacheExpire[idx] = maxExpire;
            }
            finally { _lock.ExitWriteLock(); }
        }

        public void TryFlush()
        {
            _lock.EnterWriteLock();
            try
            {
                var now = DateTime.UtcNow;
                var idx = (uint)Volatile.Read(ref _cacheIdx);
                var lastIdx = (int)((idx - 1) % _chunkCount);
                if (_cacheExpire[lastIdx] > now)
                    return;

                _cache[lastIdx].Clear();
                Volatile.Write(ref _cacheIdx, lastIdx);
            }
            finally { _lock.ExitWriteLock(); }
        }
    }
}
