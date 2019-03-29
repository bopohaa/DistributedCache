using CoreCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DistributedCache.Common.Internal
{
    internal class ClientCache<Tk, T>
    {
        private readonly BatchBlock<Tk> _request;
        private readonly ConcurrentDictionary<Tk, DateTime> _requested;
        private readonly ICache<Tk, T> _cache;
        private readonly TimeSpan _repeatRequestPeriod;
        private readonly bool _flushingCache;

        public ClientCache(BatchBlock<Tk> request, TimeSpan repeat_request_period, IEqualityComparer<Tk> comparer, ICache<Tk, T> cache = null)
        {
            _request = request;
            _repeatRequestPeriod = repeat_request_period;
            _requested = new ConcurrentDictionary<Tk, DateTime>(comparer);
            _flushingCache = cache == null;
            _cache = cache ?? new InternalCache<Tk, T>(3, comparer);
        }

        public async Task StartAsync(TimeSpan batch_interval, CancellationToken token)
        {
            var nextTryRequest = DateTime.UtcNow + _repeatRequestPeriod;

            while (true)
            {
                await Task.Delay(batch_interval, token);
                var now = DateTime.UtcNow;
                if (nextTryRequest < now)
                    nextTryRequest = TryRepeatRequest(now);

                _request.TriggerBatch();
                if (_flushingCache)
                    _cache.TryFlush();
            }
        }

        //static readonly DataflowMessageHeader SingleMessageHeader = new DataflowMessageHeader(1);

        public bool TryGetValue(Tk key, out T value, out bool expired)
        {
            var exist = _cache.TryGet(key, out value, out expired);
            if (exist && !expired)
                return true;

            if (_requested.TryAdd(key, DateTime.UtcNow + _repeatRequestPeriod))
            {
                var i = 0;
                for (; i < 10; i++)
                {
                    if (_request.Post(key))
                        break;
                    Thread.Sleep(1);
                }
                //var i = 0;
                //for (; i < 10; i++)
                //{
                //    var res = ((ITargetBlock<Tk>)_request).OfferMessage(SingleMessageHeader, key, source: null, consumeToAccept: false);
                //    if (res == DataflowMessageStatus.Accepted)
                //        break;
                //    if (res != DataflowMessageStatus.Declined)
                //        throw new OutOfMemoryException("Request queue limit exceded");
                //    Task.Delay(10).Wait();
                //}
                if (i == 10)
                    throw new OutOfMemoryException("Request queue limit exceded");
            }

            return exist;
        }

        public void AddRange(CacheValue<Tk, T>[] items)
        {
            _cache.AddRange(items);
            var end = items.Length;
            for (var i = 0; i < end; i++)
                _requested.TryRemove(items[i].Key, out var _);
        }

        private DateTime TryRepeatRequest(DateTime now)
        {
            foreach (var item in _requested)
            {
                if (item.Value < now)
                    _request.Post(item.Key);
            }

            return now + _repeatRequestPeriod;
        }
    }
}
