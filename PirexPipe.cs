using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace PirexMessage
{
    public static class PirexPipe
    {
        private static readonly ConcurrentDictionary<Type, object> _pubSubs = new ConcurrentDictionary<Type, object>();

        public static IPublisher<T> Publisher<T>()
        {
            return (IPublisher<T>)_pubSubs.GetOrAdd(typeof(T), _ => new Broker<T>());
        }

        public static ISubscriber<T> Subscriber<T>()
        {
            return (ISubscriber<T>)_pubSubs.GetOrAdd(typeof(T), _ => new Broker<T>());
        }

        public static bool Publish<T>(T payload)
        {
            if (!_pubSubs.ContainsKey(typeof(T))) return false;
            var publisher = Publisher<T>();
            return publisher.Publish(payload);
        }
        
        public static async Task<bool> PublishAsync<T>(T payload)
        {
            if (!_pubSubs.ContainsKey(typeof(T))) return false;
            var publisher = Publisher<T>();
            return await publisher.PublishAsync(payload);
        }
        
        public static bool PublishParallel<T>(T payload)
        {
            if (!_pubSubs.ContainsKey(typeof(T))) return false;
            var publisher = Publisher<T>();
            return publisher.PublishParallel(payload);
        }
        
        public static bool PublishParallel<T>(T data, ParallelOptions parallelOptions)
        {
            if (!_pubSubs.ContainsKey(typeof(T))) return false;
            var publisher = Publisher<T>();
            return publisher.PublishParallel(data, parallelOptions);
        }

        public static IDisposable Subscriber<T>(Action<T> callback)
        {
            var subscriber = Subscriber<T>();
            return subscriber.Subscribe(callback);
        }
    
        public static void Unsubscriber<T>(Action<T> callback)
        {
            if (callback == null)
                return;

            var subscriber = Subscriber<T>();
            subscriber.Unsubscribe(callback);

            // Check if broker has no subscribers and remove it
            if (subscriber is Broker<T> broker && broker.SubscriberCount == 0)
            {
                _pubSubs.TryRemove(typeof(T), out _);
            }
        }

        public static void Clear<T>()
        {
            if (_pubSubs.TryRemove(typeof(T), out var broker) && broker is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public static void ClearAll()
        {
            foreach (var broker in _pubSubs.Values)
            {
                if (broker is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _pubSubs.Clear();
        }
    }
}
