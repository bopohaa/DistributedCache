using CoreCommon;
using CoreCommon.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedCache.Common
{
    public class MessageEnumerator : IAsyncEnumerator<ChunkedStream>
    {

        public struct ReceiveStatus
        {
            public bool IsClosed;
            public bool EndOfMessage;
            public int Count;
        }

        public delegate Task<ReceiveStatus> Receive(ArraySegment<byte> buffer, CancellationToken token);

        private const int MESSAGE_BUFFER = 65536;

        private readonly Receive _receive;

        private static readonly ChunkedStreamFactory _streamFactory;

        static MessageEnumerator()
        {
            _streamFactory = new ChunkedStreamFactory(
                () => new ByteBuffer(System.Buffers.ArrayPool<byte>.Shared.Rent(MESSAGE_BUFFER)),
                chunk => System.Buffers.ArrayPool<byte>.Shared.Return(((ByteBuffer)chunk).SwapBuffer()));
        }

        public MessageEnumerator(Receive receive)
        {
            _receive = receive;
        }


        public async Task<EnumeratorResult<ChunkedStream>> MoveNextAsync(CancellationToken token)
        {
            var message = _streamFactory.Create();
            try
            {
                while (true)
                {
                    var buffer = message.StartWrite();
                    var res = await _receive(buffer, token);
                    if (res.IsClosed)
                        break;
                    message.EndWrite(buffer, res.Count);

                    if (res.EndOfMessage)
                    {
                        var rval = EnumeratorResult.CreateSuccessed(message);
                        message = null;
                        return rval;
                    }
                }
            }
            finally
            {
                message?.Dispose();
            }

            return EnumeratorResult.CreateFailed<ChunkedStream>();
        }
    }
}
