using System.Threading.Tasks;
#if PIREX_PIPE_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace PirexMessage
{
    public interface IPublisher<in T>
    {
        bool Publish(T data);
#if PIREX_PIPE_UNITASK
        UniTask<bool> PublishAsync(T data);
#endif
        bool PublishParallel(T data, ParallelOptions parallelOptions);
        bool PublishParallel(T data);
    }
}