using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedCache
{
    public interface IDistributedCacheClient<Tk, T> : IDisposable
    {
        bool TryGet(Tk key, out T value, out bool expired);

        Task StartAsync(string connection_string, CancellationToken token, TimeSpan? keepalive_interval = null, TimeSpan? reconnect_interval = null, int? reconnect_count = null, TimeSpan? connect_timeout = null);
    }
}
