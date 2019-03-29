using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedCache
{
    public struct DistributedCacheClientOptions
    {
        /// <summary>
        /// Время между повторным запросом данных (необходимо в случае если за данный промежуток времени данные не были получены)
        /// </summary>
        public TimeSpan RepeatRequestInterval;
        /// <summary>
        /// Размер пакета запрашиваемых данных
        /// </summary>
        public int BatchSize;
        /// <summary>
        /// Время между запросами данных (если запрашиваемых данных меньше чем размер пакета)
        /// </summary>
        public TimeSpan BatchInterval;
        /// <summary>
        /// Длина очереди запрашиваемых данных (возможное значение DataflowBlockOptions.Unbounded), при достижении максимального количества метод TryGet будет выдавать ошибку если запрашиваемые данные отсутствуют в локальном кеше
        /// </summary>
        public int MaxRequestedMessagesCount;

        public static DistributedCacheClientOptions Default => new DistributedCacheClientOptions {
            RepeatRequestInterval = TimeSpan.FromSeconds(60),
            BatchSize = 100,
            BatchInterval = TimeSpan.FromSeconds(1),
            MaxRequestedMessagesCount = 1000
        };
    }
}
