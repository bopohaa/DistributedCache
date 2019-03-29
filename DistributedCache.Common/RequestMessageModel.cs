using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DistributedCache.Common
{
    [ProtoContract]
    public struct RequestMessageModel
    {
        [ProtoMember(1)]
        public byte[][] Keys;

    }
}
