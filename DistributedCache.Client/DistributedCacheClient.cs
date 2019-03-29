using CoreCommon;
using CoreCommon.Common;
using DistributedCache.Common;
using DistributedCache.Common.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DistributedCache
{
    public class DistributedCacheClient<Tk, T> : IDistributedCacheClient<Tk, T>, IDisposable
    {
        private const int MESSAGE_BUFFER = 65536;

        private readonly TimeSpan _batchInterval;
        private readonly BatchBlock<Tk> _batchBlock;
        private readonly ClientCache<Tk, T> _cache;
        private InternalClient _client;
        private ChunkedStreamFactory _streamFactory;

        public bool IsConnected => _client?.IsConnected ?? false;

        /// <summary>
        /// Клиент распределенного кеша
        /// </summary>
        /// <param name="options">Настройки работы клиента</param>
        /// <param name="comparer">Функция сравнения ключей</param>
        /// <param name="cache">Внешний кеш. Если не задат то будет использоваться свой внутренний кеш, уникальный на каждый экземпляр этого класса. При заданном значении сторонний код берет на себя переодический вызов ICache.TryFlush</param>
        public DistributedCacheClient(DistributedCacheClientOptions options, IEqualityComparer<Tk> comparer, ICache<Tk, T> cache = null)
        {
            _batchInterval = options.BatchInterval;
            _batchBlock = new BatchBlock<Tk>(options.BatchSize, new GroupingDataflowBlockOptions() { BoundedCapacity = options.MaxRequestedMessagesCount });

            _cache = new ClientCache<Tk, T>(_batchBlock, options.RepeatRequestInterval, comparer, cache);
            _client = null;

            _streamFactory = new ChunkedStreamFactory(
                () => new ByteBuffer(System.Buffers.ArrayPool<byte>.Shared.Rent(MESSAGE_BUFFER)),
                chunk => System.Buffers.ArrayPool<byte>.Shared.Return(((ByteBuffer)chunk).SwapBuffer()));
        }

        /// <summary>
        /// Запуск локального кеша. Необходимо выполнить до начала работы с кешем. Завершение операции может быть только при отмене или исключительной ситуации что является завершением работы локального кеша
        /// </summary>
        /// <param name="connection_string">Строка подключения имеет формат "dc://host_or_ip/cache_endpoint"</param>
        /// <param name="keepalive_interval">Интервал времени для проверки подключения с сервером, по умолчанию равен 90 сек</param>
        /// <param name="reconnect_interval">Время ожидания между подключениями в случае потери соединения, по умолчанию равен 3 сек</param>
        /// <param name="reconnect_count">Количество переподключений при разрыве связи. По истечению метод будет завершен с ошибкой, по умолчанию равен 60</param>
        /// <param name="connect_timeout">Время ожидания подключения к серверу</param>
        /// <param name="token">Токен отмены выполнения</param>
        /// <returns></returns>
        public async Task StartAsync(string connection_string, CancellationToken token, TimeSpan? keepalive_interval = null, TimeSpan? reconnect_interval = null, int? reconnect_count = null, TimeSpan? connect_timeout = null)
        {
            if (_client != null)
                throw new InvalidOperationException();

            var conn = DistributedCacheConnectionString.Parse(connection_string.Split(',').First(),
                keepalive_interval ?? TimeSpan.FromSeconds(90), reconnect_interval ?? TimeSpan.FromSeconds(3), reconnect_count ?? 60, connect_timeout ?? TimeSpan.FromSeconds(10));
            var builder = new UriBuilder("ws", conn.Host, conn.Port, conn.Path);

            _client = new InternalClient(builder.Uri, conn.ConnectionOptions.KeepaliveInterval, conn.ConnectionOptions.ReconnectInterval, conn.ConnectionOptions.ConnectTimeout, conn.ConnectionOptions.ReconnectCount);
            await _client.ConnectAsync(token);

            var cache = _cache.StartAsync(_batchInterval, token);
            var bacth = StartBatching(token);
            var receive = StartReceive(token);

            await Task.WhenAny(cache, bacth, receive);
        }

        /// <summary>
        /// Попытка получения значения по ключу из кеша. В случае отсутствия или устаревания значения будет предпринята попытка получения значения из удаленного кеша
        /// </summary>
        /// <param name="key">Ключ значения</param>
        /// <param name="value">Найденое значение</param>
        /// <param name="expired">Признак устаревания значения в кеше</param>
        /// <returns>Было ли получено значение. Будет возвращено true даже в том случае если значение устарело</returns>
        public bool TryGet(Tk key, out T value, out bool expired)
        {
            return _cache.TryGetValue(key, out value, out expired);
        }

        public void Dispose()
        {
            _batchBlock.Complete();
        }

        private async Task StartBatching(CancellationToken token)
        {
            using (var buff = new MemoryStream())
                while (await _batchBlock.OutputAvailableAsync(token))
                {
                    if (!_batchBlock.TryReceive(out var chunk))
                        continue;
                    var keys = new byte[chunk.Length][];

                    for (var i = 0; i < chunk.Length; i++)
                    {
                        buff.SetLength(0);
                        ProtoBuf.Serializer.Serialize(buff, chunk[i]);
                        keys[i] = buff.ToArray();
                    }
                    var message = new RequestMessageModel() { Keys = keys };
                    using (var send = _streamFactory.Create())
                    {
                        ProtoBuf.Serializer.Serialize(send, message);
                        await _client.SendAsync(send, token);
                    }
                }
        }

        private async Task StartReceive(CancellationToken token)
        {
            EnumeratorResult<ChunkedStream> res;
            while ((res = await _client.ReceiveAsync(token)).Success)
                using (res.Value)
                using (var reader = new MemoryStreamReader())
                {
                    var response = ProtoBuf.Serializer.Deserialize<ResponseMessageModel>(res.Value);
                    var items = new CacheValue<Tk, T>[response.Messages.Count];
                    var idx = 0;

                    foreach (var message in response.Messages)
                    {
                        reader.SetBuffer(message.Key);
                        reader.SetLength(message.Key.Length);
                        var key = ProtoBuf.Serializer.Deserialize<Tk>(reader);
                        reader.SetBuffer(message.Value);
                        reader.SetLength(message.Value.Length);
                        var val = ProtoBuf.Serializer.Deserialize<T>(reader);
                        items[idx++] = new CacheValue<Tk, T>(key, val, message.ExpiredAtSeconds);

                    }
                    _cache.AddRange(items);
                }
        }

        private void GetKey<Tk1>(byte[] key, out Tk1 v, MemoryStreamReader reader)
        {
            reader.SetBuffer(key);
            reader.SetLength(key.Length);
            v = ProtoBuf.Serializer.Deserialize<Tk1>(reader);
        }
        private void GetKey(byte[] key, out byte[] v, MemoryStreamReader reader)
        {
            v = key;
        }

    }
}
