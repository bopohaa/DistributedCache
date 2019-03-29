using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedCache.Common
{

    public interface ICache<Tk, T>
    {
        /// <summary>
        /// Получить значение в кеше по ключу. В случае если значение есть, но срок его хранения завершен значение все равно будет возвращено.
        /// </summary>
        /// <param name="key">значение ключа в кеше</param>
        /// <param name="value">возвращаемое значение</param>
        /// <param name="expired">признак того что срок хранения значения в кеше завершен</param>
        /// <returns></returns>
        bool TryGet(Tk key, out T value, out bool expired);

        /// <summary>
        /// Добавить множество значений в кеш
        /// </summary>
        /// <param name="items">список добавляемых значений</param>
        void AddRange(CacheValue<Tk, T>[] items);

        /// <summary>
        /// Попытка сброса устаревших значений из кеша. 
        /// Рекомендуется переодически вызывать данный метод (переодичность вызвова не больше максимального времени жизни значения)
        /// </summary>
        void TryFlush();

    }
}
