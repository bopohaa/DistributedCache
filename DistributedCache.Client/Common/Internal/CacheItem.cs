using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedCache.Common.Internal
{
    internal struct CacheItem<T>
    {
        public T Payload;
        public DateTime Expired;
    }
}
