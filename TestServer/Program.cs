using Microsoft.AspNetCore.Hosting;
using DistributedCache;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using CoreCommon;
using DistributedCache.Common;
using System.IO;

namespace TestServer
{
    class Program
    {
        private static readonly ServerCache _cache = new ServerCache(TimeSpan.FromSeconds(120), 65536);

        static void Main(string[] args)
        {
            var cancel = new CancellationTokenSource();
            var cache = CacheHandler(cancel.Token);

            var port = args.Length > 0 ? int.Parse(args[0]) : 9090;

            new WebHostBuilder()
                .UseKestrel(op => ServerConfigure(port, op))
                .Configure(AppConfigure)
                .Build()
                .Run();

            cancel.Cancel();
        }

        static void AppConfigure(IApplicationBuilder app)
        {
            var testPath = "/test";
            var testHandler = new AdServerHandler(_cache);

            app
                .Use(HandleExceptions)
                .EnableAdServerCache()
                .AddAdServerCache(testPath, testHandler)
                ;
        }

        static void ServerConfigure(int port, KestrelServerOptions opt)
        {
            opt.ListenAnyIP(port);
        }

        static async Task CacheHandler(CancellationToken cancel)
        {
            var keys = _cache.PutItems(Array.Empty<CacheValue>());

            while (true)
            {
                try
                {
                    var items = Resolve(keys);
                    keys = _cache.PutItems(items);
                    if (keys.Count > 0)
                        Console.WriteLine($"Resolved {keys.Count} keys");
                    await Task.Delay(10, cancel);
                }
                catch (TaskCanceledException) { throw; }
                catch (Exception)
                {

                }
            }
        }

        static ICollection<CacheValue> Resolve(ICollection<byte[]> keys)
        {
            if (keys.Count == 0)
                return Array.Empty<CacheValue>();
            var result = new List<CacheValue>();
            var comparer = new ByteArrayComparer();

            using (var ms = new MemoryStream())
                foreach (var key in keys)
                {
                    ms.SetLength(0);
                    ms.Write(key, 0, key.Length);
                    ms.Position = 0;
                    var k = ProtoBuf.Serializer.Deserialize<byte[]>(ms);
                    var hash = comparer.GetHashCode(k);
                    var random = new Random(hash);
                    var length = Math.Max((hash & 0xff) << 8, 256);
                    var data = new byte[length];
                    random.NextBytes(data);
                    ms.SetLength(0);
                    ProtoBuf.Serializer.Serialize(ms, data);

                    result.Add(new CacheValue(key, ms.ToArray(), TimeSpan.FromSeconds(60)));
                }

            return result;
        }

        static async Task HandleExceptions(HttpContext context, Func<Task> next)
        {
            try
            {
                await next();
            }
            catch (AggregateException ex)
            {
                Console.WriteLine("unhandled exceptions:");
                ex.Handle(e =>
                {
                    Console.WriteLine("\t" + e.Message);
                    return true;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("unhandled exception: " + ex.Message);
            }
        }
    }
}
