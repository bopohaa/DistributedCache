using CoreCommon.Common;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedCache.Common.Internal
{
    internal class InternalClient
    {
        private readonly Uri _connectionString;
        private readonly TimeSpan _keepAliveInterval;
        private Task<Task> _connect;
        private readonly TimeSpan _reconnectInterval;
        private readonly int _reconnectCount;
        private readonly TimeSpan _connectTimeout;
        private IInteract _interact;
        private IAsyncEnumerator<ChunkedStream> _receive;

        public bool IsConnected => _interact?.IsConnected ?? false;

        public InternalClient(Uri connection_string, TimeSpan keep_alive_interval, TimeSpan reconnect_interval, TimeSpan connect_timeout, int reconnect_count)
        {
            _connectionString = connection_string;
            _keepAliveInterval = keep_alive_interval;
            _connect = null;
            _reconnectInterval = reconnect_interval;
            _reconnectCount = reconnect_count;
            _connectTimeout = connect_timeout;
        }

        public async Task ConnectAsync(CancellationToken token)
        {
            if (_connect != null && !_connect.Result.IsFaulted && !_connect.Result.IsCanceled)
                throw new InvalidOperationException();

            await await Reconnect(token);
        }

        public async Task SendAsync(ChunkedStream message, CancellationToken token)
        {
            var connect = _connect.Result;
            loop:
            try
            {
                if (!connect.IsCompleted)
                    await connect;
                await _interact.Send(message, token);
            }
            catch (TaskCanceledException) { throw; }
            catch
            {
                connect = await Reconnect(token);
                goto loop;
            }
        }

        public async Task<EnumeratorResult<ChunkedStream>> ReceiveAsync(CancellationToken token)
        {
            var connect = _connect.Result;
            loop:
            try
            {
                if (!connect.IsCompleted)
                    await connect;
                return await _receive.MoveNextAsync(token);
            }
            catch (TaskCanceledException) { throw; }
            catch
            {
                connect = await Reconnect(token);
                goto loop;

            }
        }

        private Task<Task> Reconnect(CancellationToken token)
        {
            var connect = Volatile.Read(ref _connect);
            if (connect != null && !connect.Result.IsCompleted)
                return connect;

            var task = new Task<Task>(() => Connect(token));
            var res = Interlocked.CompareExchange(ref _connect, task, connect);
            if (res == connect)
            {
                task.Start();

                return task;
            }
            else
                task.Dispose();

            return res;
        }

        private async Task Connect(CancellationToken token)
        {
            var reconnectCount = _reconnectCount;
            loop:
            var client = new ClientWebSocket();
            try
            {
                client.Options.KeepAliveInterval = _keepAliveInterval;
                await Task.WhenAny(Task.Delay(_connectTimeout, token), client.ConnectAsync(_connectionString, token));
                if (client.State != WebSocketState.Open)
                    throw new TimeoutException();
                if (_interact != null)
                    try { _interact.Dispose(); } catch { }

                _interact = new Interact(client);
                _receive = _interact.Receive();
            }
            catch (TaskCanceledException) { throw; }
            catch
            {
                try { client.Dispose(); } catch { }
                await Task.Delay(_reconnectInterval, token);
                if (--reconnectCount == 0)
                    throw;
                goto loop;
            }
        }

    }
}
