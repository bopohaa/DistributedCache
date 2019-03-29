using DistributedCache.Common;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using System.Threading;
using DistributedCache.Common.Internal;
using System.Collections;
using System.IO;

namespace DistributedCache.Middleware
{
    public class ServerMiddleware
    {
        private readonly PathString _path;
        private readonly IAdServerHandler _handler;
        private readonly RequestDelegate _next;

        public ServerMiddleware(RequestDelegate next, PathString path, IAdServerHandler handler)
        {
            _next = next;
            _path = path;
            _handler = handler;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path == _path)
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    using(var cntx = new Interact(webSocket))
                    {
                        await _handler.Run(cntx);
                        await cntx.Close(CancellationToken.None);
                    }
                }
                else
                    context.Response.StatusCode = 400;
            }
            else
                await _next(context);
        }
    }
}
