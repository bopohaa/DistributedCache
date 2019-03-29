using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedCache.Common
{
    [ProtoContract]
    public struct CacheValue<Tk, Tv>
    {
        [ProtoMember(1)]
        public Tk Key;
        [ProtoMember(2)]
        public Tv Value;
        [ProtoMember(3)]
        public ushort ExpiredAtSeconds;

        public CacheValue(Tk key, Tv value, ushort expired_at_seconds)
        {
            Key = key;
            Value = value;
            ExpiredAtSeconds = expired_at_seconds;
        }
    }

    [ProtoContract]
    public struct CacheValue
    {
        private const long MAX_EXPIRED_AT = TimeSpan.TicksPerSecond * 65535;
        [ProtoMember(1)]
        public byte[] Key;
        [ProtoMember(2)]
        public byte[] Value;
        [ProtoMember(3)]
        public ushort ExpiredAtSeconds;

        public CacheValue(byte[] key, byte[] value, TimeSpan expired_at)
        {
            Key = key;
            Value = value;
            var expiredAd = expired_at.Ticks;
            if (expired_at.Ticks > MAX_EXPIRED_AT)
                throw new ArgumentOutOfRangeException(nameof(expired_at));
            ExpiredAtSeconds = (ushort)(expiredAd / TimeSpan.TicksPerSecond);
        }
    }

}
