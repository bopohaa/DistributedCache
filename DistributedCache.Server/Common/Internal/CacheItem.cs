using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedCache.Common.Internal
{
    internal struct CacheItem
    {
        public byte[] Payload;
        public DateTime Expired;
    }
}
