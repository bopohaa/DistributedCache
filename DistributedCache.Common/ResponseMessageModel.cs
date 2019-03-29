using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedCache.Common
{
    [ProtoContract]
    public struct ResponseMessageModel
    {
        [ProtoMember(1)]
        public ICollection<CacheValue> Messages;

        public static ResponseMessageModel Create(ICollection<CacheValue> messages)
        {
            return new ResponseMessageModel { Messages = messages };
        }
    }
}
