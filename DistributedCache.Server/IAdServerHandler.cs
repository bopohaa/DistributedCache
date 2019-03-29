using DistributedCache.Common;
using DistributedCache.Common.Internal;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace DistributedCache
{
    /// <summary>
    /// Интерфейс обработчика распределенного кеша
    /// </summary>
    public interface IAdServerHandler
    {
        /// <summary>
        /// Запуск процесса обработки вохдящих и исходящих клиентских запросов
        /// </summary>
        /// <param name="context">контекст работы с подключением</param>
        /// <returns></returns>
        Task Run(IInteract context);

    }
}
