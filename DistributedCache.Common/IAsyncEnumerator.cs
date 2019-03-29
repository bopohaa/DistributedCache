using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedCache.Common
{
    public interface IAsyncEnumerator<T>
    {
        Task<EnumeratorResult<T>> MoveNextAsync(CancellationToken token);
    }

    public struct EnumeratorResult<T>
    {
        public readonly bool Success;
        public readonly T Value;

        internal EnumeratorResult(bool success, T value)
        {
            Success = success;
            Value = value;
        }
    }

    public struct EnumeratorResult
    {
        public static EnumeratorResult<T> CreateSuccessed<T>(T value)
        {
            return new EnumeratorResult<T>(true, value);
        }

        public static EnumeratorResult<T> CreateFailed<T>()
        {
            return new EnumeratorResult<T>(false, default(T));
        }

    }
}
