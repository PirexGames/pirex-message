using System.Threading.Tasks;

namespace PirexMessage
{
    public interface IPublisher<in T>
    {
        bool Publish(T data);
        Task<bool> PublishAsync(T data);
        bool PublishParallel(T data, ParallelOptions parallelOptions);
        bool PublishParallel(T data);
    }
}