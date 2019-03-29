using CoreCommon.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedCache.Common
{
    /// <summary>
    /// Контекст работы с подключением
    /// </summary>
    public interface IInteract : IDisposable
    {
        /// <summary>
        /// Признак того что подлкючения активно
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Отправка "сырого" сообщения
        /// </summary>
        /// <param name="message">сообщение</param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task Send(ChunkedStream message, CancellationToken token);

        /// <summary>
        /// Инициирует поток принимаемых "сырых" сообщений
        /// </summary>
        /// <returns></returns>
        IAsyncEnumerator<ChunkedStream> Receive();

        /// <summary>
        /// Закрыть подключение
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        Task Close(CancellationToken token);
    }
}
