using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace PirexMessage
{
    public static class PirexPipe
    {
        private static readonly ConcurrentDictionary<Type, object> BrokersGeneric = new ConcurrentDictionary<Type, object>();

        public static IPublisher<T> Publisher<T>()
        {
            return (IPublisher<T>)BrokersGeneric.GetOrAdd(typeof(T), _ => new Broker<T>());
        }

        public static ISubscriber<T> Subscriber<T>()
        {
            return (ISubscriber<T>)BrokersGeneric.GetOrAdd(typeof(T), _ => new Broker<T>());
        }

        public static bool Publish<T>(T payload)
        {
            if (!BrokersGeneric.ContainsKey(typeof(T))) return false;
            var publisher = Publisher<T>();
            return publisher.Publish(payload);
        }
        
#if PIREX_PIPE_UNITASK
        public static async UniTask<bool> PublishAsync<T>(T payload)
        {
            if (!BrokersGeneric.ContainsKey(typeof(T))) return false;
            var publisher = Publisher<T>();
            return await publisher.PublishAsync(payload);
        }
#endif
        
        public static bool PublishParallel<T>(T payload)
        {
            if (!BrokersGeneric.ContainsKey(typeof(T))) return false;
            var publisher = Publisher<T>();
            return publisher.PublishParallel(payload);
        }
        
        public static bool PublishParallel<T>(T data, ParallelOptions parallelOptions)
        {
            if (!BrokersGeneric.ContainsKey(typeof(T))) return false;
            var publisher = Publisher<T>();
            return publisher.PublishParallel(data, parallelOptions);
        }

        public static IDisposable Subscribe<T>(Action<T> callback)
        {
            var subscriber = Subscriber<T>();
            return subscriber.Subscribe(callback);
        }
    
        public static void Unsubscribe<T>(Action<T> callback)
        {
            if (callback == null)
                return;

            var subscriber = Subscriber<T>();
            subscriber.Unsubscribe(callback);

            // Check if broker has no subscribers and remove it
            if (subscriber is Broker<T> { SubscriberCount: 0 })
            {
                BrokersGeneric.TryRemove(typeof(T), out _);
            }
        }

        public static void Cleanup<T>()
        {
            if (BrokersGeneric.TryRemove(typeof(T), out var broker) && broker is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public static void ClearAll()
        {
            foreach (var broker in BrokersGeneric.Values)
            {
                if (broker is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            BrokersGeneric.Clear();
        }
    }
}
