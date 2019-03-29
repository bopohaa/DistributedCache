using DistributedCache.Common;
using CoreCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using CoreCommon.Common;

namespace DistributedCache
{
    public class AdServerHandler : IAdServerHandler
    {
        private const int MAX_MESSAGES_IN_QUEUE = 10;
        private const int DELAY_BETWEEN_MESSAGE_RECEIVE_MS = 1000;
        private const int MESSAGE_BUFFER = 65536;

        private static readonly ChunkedStreamFactory _streamFactory;

        private readonly IServerCache _cache;
        private readonly LinkedList<BufferBlock<ResponseMessageModel>> _clients;
        private readonly ReaderWriterLockSlim _lock;

        static AdServerHandler()
        {
            _streamFactory = new ChunkedStreamFactory(
                () => new ByteBuffer(System.Buffers.ArrayPool<byte>.Shared.Rent(MESSAGE_BUFFER)),
                chunk => System.Buffers.ArrayPool<byte>.Shared.Return(((ByteBuffer)chunk).SwapBuffer()));
        }

        public AdServerHandler(IServerCache cache)
        {
            _cache = cache;
            _clients = new LinkedList<BufferBlock<ResponseMessageModel>>();
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _cache.OnItems += Cache_OnItems;
        }

        public async Task Run(IInteract context)
        {
            using (var cancel = new CancellationTokenSource())
            {
                var client = RegisterClient();

                var receive = Receive(context, _cache, client.Value, cancel.Token);
                var send = Send(context, client.Value, cancel.Token);

                try
                {
                    await await Task.WhenAny(receive, send);
                }
                finally
                {
                    cancel.Cancel();

                    UnregisterClient(client);
                }
            }
        }

        private static async Task Receive(IInteract context, IServerCache cache, BufferBlock<ResponseMessageModel> messages, CancellationToken token)
        {
            var it = context.Receive();
            EnumeratorResult<ChunkedStream> messageStream;
            while ((messageStream = await it.MoveNextAsync(token)).Success)
                using (messageStream.Value)
                {
                    var message = ProtoBuf.Serializer.Deserialize<RequestMessageModel>(messageStream.Value);
                    var items = cache.TryGetItems(message.Keys);
                    if (items.Count > 0)
                    {
                        var mes = ResponseMessageModel.Create(items);
                        if (!messages.Post(mes) && messages.TryReceive(out var last))
                            messages.Post(mes);
                    }
                }
        }

        private static async Task Send(IInteract context, BufferBlock<ResponseMessageModel> messages, CancellationToken token)
        {
            while (await messages.OutputAvailableAsync(token))
                using (var stream = _streamFactory.Create())
                {
                    var message = await messages.ReceiveAsync(token);
                    ProtoBuf.Serializer.Serialize(stream, message);
                    await context.Send(stream, token);
                }
        }

        private void Cache_OnItems(ICollection<CacheValue> items)
        {
            _lock.EnterReadLock();
            try
            {
                foreach (var client in _clients)
                {
                    client.Post(ResponseMessageModel.Create(items));
                }
            }
            finally { _lock.ExitReadLock(); }
        }

        private LinkedListNode<BufferBlock<ResponseMessageModel>> RegisterClient()
        {
            _lock.EnterWriteLock();
            try
            {
                return _clients.AddLast(new BufferBlock<ResponseMessageModel>(new DataflowBlockOptions() { BoundedCapacity = MAX_MESSAGES_IN_QUEUE, EnsureOrdered = true }));
            }
            finally { _lock.ExitWriteLock(); }
        }

        private void UnregisterClient(LinkedListNode<BufferBlock<ResponseMessageModel>> client)
        {
            client.Value.Complete();
            _lock.EnterWriteLock();
            try
            {
                _clients.Remove(client);

            }
            finally { _lock.ExitWriteLock(); }
        }

    }
}
