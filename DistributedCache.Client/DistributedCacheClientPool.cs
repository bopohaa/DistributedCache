using DistributedCache.Common;
using DistributedCache.Common.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedCache
{
    public class DistributedCacheClientPool<Tk, T> : IDistributedCacheClient<Tk, T>, IDisposable
    {
        private readonly DistributedCacheClientOptions _options;
        private readonly ICache<Tk, T> _cache;
        private readonly IEqualityComparer<Tk> _comparer;
        private readonly Action<int, Exception> _catch;
        private readonly bool _flushingCache;
        private DistributedCacheClient<Tk, T>[] _pool;
        private CancellationTokenSource _cancel;

        public DistributedCacheClientPool(DistributedCacheClientOptions options, IEqualityComparer<Tk> comparer, Action<int, Exception> catch_client_exception = null, ICache<Tk, T> cache = null)
        {
            _options = options;
            _cache = cache ?? new InternalCache<Tk, T>(3, comparer);
            _flushingCache = cache == null;
            _comparer = comparer;
            _catch = catch_client_exception;
            _pool = null;
            _cancel = null;
        }

        public async Task StartAsync(string connection_string, CancellationToken token, TimeSpan? keepalive_interval = null, TimeSpan? reconnect_interval = null, int? reconnect_count = null, TimeSpan? connect_timeout = null)
        {
            if (_cancel != null)
                throw new InvalidOperationException();
            _cancel = new CancellationTokenSource();

            var connectionStrings = connection_string.Split(',');
            var tasks = new Task[connectionStrings.Length + 1];

            _pool = connectionStrings.Select(c => new DistributedCacheClient<Tk, T>(_options, _comparer, _cache)).ToArray();
            for (var i = 0; i < connectionStrings.Length; ++i)
            {
                var task = _pool[i].StartAsync(connectionStrings[i], _cancel.Token, keepalive_interval, reconnect_interval, reconnect_count, connect_timeout);
                tasks[i] = task;
            }
            tasks[connectionStrings.Length] = Task.Delay(_options.BatchInterval, token);

            while (!token.IsCancellationRequested)
            {
                var task = await Task.WhenAny(tasks);
                var idx = Array.FindIndex(tasks, t => t == task);
                if (idx == connectionStrings.Length && _flushingCache)
                {
                    _cache.TryFlush();
                    tasks[idx] = Task.Delay(_options.BatchInterval, token);
                    continue;
                }

                if (task.IsFaulted && _catch != null)
                    _catch(idx, task.Exception);

                var client = _pool[idx];
                client.Dispose();
                client = new DistributedCacheClient<Tk, T>(_options, _comparer, _cache);
                _pool[idx] = client;
                task = client.StartAsync(connectionStrings[idx], token, keepalive_interval, reconnect_interval, reconnect_count);
                tasks[idx] = task;
            }
            _cancel.Cancel();
        }

        public void Dispose()
        {
            _cancel.Dispose();
            foreach (var client in _pool ?? Array.Empty<DistributedCacheClient<Tk, T>>())
                client.Dispose();
            _pool = null;
            _cancel = null;
        }

        public bool TryGet(Tk key, out T value, out bool expired)
        {
            var pool = _pool;
            if (pool == null)
                throw new InvalidOperationException();

            var cnt = _comparer.GetHashCode(key);
            for (uint i = 0; i < pool.Length; ++i)
            {
                var idx = (cnt + i) % pool.Length;
                var client = pool[idx];
                if (client.IsConnected)
                    return client.TryGet(key, out value, out expired);
            }

            return _cache.TryGet(key, out value, out expired);
        }
    }
}
