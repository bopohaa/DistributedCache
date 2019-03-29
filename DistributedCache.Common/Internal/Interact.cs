using CoreCommon;
using CoreCommon.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedCache.Common.Internal
{
    public class Interact : IInteract, IDisposable
    {
        private const int SEND_TIMEOUT_MS = 30000;

        private readonly WebSocket _socket;

        public bool IsConnected => _socket.State == WebSocketState.Open;


        public Interact(WebSocket socket)
        {
            _socket = socket;
        }

        public async Task Send(ChunkedStream message, CancellationToken token)
        {
            var length = message.Length;
            ArraySegment<byte> chunk;

            while (length > 0 && (chunk = message.StartRead()).Count > 0)
            {
                length -= chunk.Count;
                if (length < 0)
                    throw new IndexOutOfRangeException();

                using (var timeout = new CancellationTokenSource(SEND_TIMEOUT_MS))
                using (var ctx = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token))
                    await _socket.SendAsync(chunk, WebSocketMessageType.Binary, length == 0, ctx.Token);
                message.EndRead(chunk, chunk.Count);
            }
            if (length != 0)
                throw new InvalidOperationException();
        }

        public IAsyncEnumerator<ChunkedStream> Receive()
        {
            return new MessageEnumerator(
                async (buffer, token) =>
                {
                    var value = await _socket.ReceiveAsync(buffer, token);
                    return new MessageEnumerator.ReceiveStatus() { IsClosed = value.CloseStatus.HasValue, Count = value.Count, EndOfMessage = value.EndOfMessage };
                });
        }

        public Task Close(CancellationToken token)
        {
            return _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", token);
        }

        public void Dispose()
        {
            if (!_socket.CloseStatus.HasValue)
                _socket.Abort();
        }
    }
}
