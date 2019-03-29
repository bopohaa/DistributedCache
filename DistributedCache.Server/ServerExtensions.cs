using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using DistributedCache.Common;
using DistributedCache.Middleware;

namespace DistributedCache
{
    public static class ServerExtensions
    {
        public static IApplicationBuilder EnableAdServerCache(this IApplicationBuilder app)
        {
            return app.UseWebSockets(new WebSocketOptions() { KeepAliveInterval = TimeSpan.FromSeconds(120) });
        }

        public static IApplicationBuilder AddAdServerCache(this IApplicationBuilder app, PathString server_path, IAdServerHandler handler)
        {
            return app.UseMiddleware<ServerMiddleware>(server_path, handler);
        }
    }
}
