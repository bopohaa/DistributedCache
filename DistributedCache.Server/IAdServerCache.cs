using DistributedCache.Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DistributedCache
{
    /// <summary>
    /// Интерфейс сервиса работы активного кеша
    /// </summary>
    public interface IServerCache
    {
        /// <summary>
        /// Получить список значений по списку ключей
        /// </summary>
        /// <param name="keys">список ключей</param>
        /// <returns>список значений соответствующий списку ключей</returns>
        ICollection<CacheValue> TryGetItems(IEnumerable<byte[]> keys);
        /// <summary>
        /// Обновить хранимые значения
        /// </summary>
        /// <param name="items">список обновляемых значений, может быть пустым</param>
        /// <returns>список ключей значений которые необходимо обновить</returns>
        ICollection<byte[]> PutItems(ICollection<CacheValue> items);
        /// <summary>
        /// Событие обновления значений (нобходимо для оповещения всех клиентов об обновлении)
        /// </summary>
        event Action<ICollection<CacheValue>> OnItems;
    }
}
