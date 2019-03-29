using DistributedCache.Common;
using CoreCommon;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CoreCommon.Common;
using DistributedCache;

namespace TestClient
{
    class Program
    {
        private static ConcurrentDictionary<byte[], DateTime> _requested = new ConcurrentDictionary<byte[], DateTime>(new ByteArrayComparer());

        static async Task Main(string[] args)
        {
            var cancel = new CancellationTokenSource();
            var token = cancel.Token;

            var options = new DistributedCacheClientOptions
            {
                RepeatRequestInterval = TimeSpan.FromSeconds(600),
                BatchSize = 100,
                BatchInterval = TimeSpan.FromSeconds(1),
                MaxRequestedMessagesCount = 100000
            };

            var conn = "dc://localhost:9090/test,dc://localhost:9091/test?keepalive_interval=60000&reconnect_interval=3000&reconnect_count=120&connect_timeout=3000";

            var messagesPerThread = 100;
            using (var client = new DistributedCacheClientPool<byte[], byte[]>(options, new ByteArrayComparer()))
            {
                var receiveTask = client.StartAsync(conn, token, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(3), 60, TimeSpan.FromSeconds(10));
                var sendTask = StartSend(client, messagesPerThread, token);

                Task.WhenAny(receiveTask, sendTask).Wait();
            }
            
        }

        private static async Task StartSend(IDistributedCacheClient<byte[], byte[]> client, int messages_per_thread, CancellationToken token)
        {
            var seed = 0;
            while (true)
            {
                var now = DateTime.UtcNow;
                var count = await Send(client, messages_per_thread, seed, token);
                Console.WriteLine($"Receive {count} keys after: {(DateTime.UtcNow - now).TotalSeconds} sec");
                seed += 256;
            }
        }

        private static async Task<int> Send(IDistributedCacheClient<byte[], byte[]> client, int messages_per_thread, int seed, CancellationToken token)
        {
            var now = DateTime.UtcNow;
            var requested = new Dictionary<byte[], DateTime>(new ByteArrayComparer());
            var comparer = new ByteArrayComparer();
            var rand = new Random(seed);

            for (var i = 0; i < messages_per_thread; i++)
            {
                var key = CreateId(rand);
                if (client.TryGet(key, out var data, out var expired) && !expired)
                    requested.Remove(key, out var _);
                else
                    requested.TryAdd(key, now);
            }
            var rval = requested.Count;

            for (var i = 0; i < 10000 && requested.Count > 0; i++)
            {
                foreach (var key in requested.Keys.ToArray())
                {
                    if (client.TryGet(key, out var data, out var expired) && !expired)
                    {
                        requested.Remove(key, out var _);
                        var hash = comparer.GetHashCode(key);
                        var random = new Random(hash);
                        var length = Math.Max((hash & 0xff) << 8, 256);
                        var d = new byte[length];
                        random.NextBytes(d);

                        if (!comparer.Equals(d, data))
                            throw new InvalidOperationException();
                    }
                }
                await Task.Delay(100, token);
            }

            if (requested.Count != 0)
                throw new Exception();


            return rval;
        }

        static byte[] CreateId(Random rnd)
        {
            var val = rnd.Next();
            return new byte[] { (byte)(val & 0xff), (byte)((val >> 8) & 0xff) };
        }
    }
}