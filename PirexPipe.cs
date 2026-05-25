using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
#if PIREX_PIPE_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace PirexMessage
{
    public static class PirexPipe
    {
        private static readonly ConcurrentDictionary<Type, object> BrokersGeneric
            = new ConcurrentDictionary<Type, object>();

        // ── Internal accessors ───────────────────────────────────────────────────
        // GetOrAdd lambda captures no local variables → compiler caches delegate per type.

        public static IPublisher<T> Publisher<T>()
        {
            return (IPublisher<T>)BrokersGeneric.GetOrAdd(typeof(T), _ => new Broker<T>());
        }

        public static ISubscriber<T> Subscriber<T>()
        {
            return (ISubscriber<T>)BrokersGeneric.GetOrAdd(typeof(T), _ => new Broker<T>());
        }

        // ── Publish ──────────────────────────────────────────────────────────────
        // TryGetValue = single dictionary lookup (no TOCTOU, no extra GetOrAdd).

        public static bool Publish<T>(T payload)
        {
            if (!BrokersGeneric.TryGetValue(typeof(T), out var broker)) return false;
            return ((IPublisher<T>)broker).Publish(payload);
        }

#if PIREX_PIPE_UNITASK
        public static UniTask<bool> PublishAsync<T>(T payload)
        {
            if (!BrokersGeneric.TryGetValue(typeof(T), out var broker))
                return UniTask.FromResult(false);
            return ((IPublisher<T>)broker).PublishAsync(payload);
        }
#endif

        public static bool PublishParallel<T>(T payload)
        {
            if (!BrokersGeneric.TryGetValue(typeof(T), out var broker)) return false;
            return ((IPublisher<T>)broker).PublishParallel(payload);
        }

        public static bool PublishParallel<T>(T data, ParallelOptions parallelOptions)
        {
            if (!BrokersGeneric.TryGetValue(typeof(T), out var broker)) return false;
            return ((IPublisher<T>)broker).PublishParallel(data, parallelOptions);
        }

        // ── Subscribe / Unsubscribe ───────────────────────────────────────────────

        /// <param name="ordered">
        /// false (default): dispatch order is unspecified — fastest path (swap-remove).
        /// true:            dispatch in subscription order — use when order matters.
        /// Note: if a broker for T already exists its ordering mode is fixed at creation.
        /// </param>
        public static IDisposable Subscribe<T>(Action<T> callback, Predicate<T> filter = null, bool ordered = false)
        {
            // GetOrAdd: if broker doesn't exist, create with requested ordering.
            // Lambda is cached per (T, ordered) — but ordered is a captured variable here.
            // To avoid closure, we use TryGetValue + TryAdd pattern:
            if (!BrokersGeneric.TryGetValue(typeof(T), out var existing))
            {
                var broker = new Broker<T>(ordered);
                existing = BrokersGeneric.GetOrAdd(typeof(T), broker);
            }
            return ((ISubscriber<T>)existing).Subscribe(callback, filter);
        }

        public static void Unsubscribe<T>(Action<T> callback)
        {
            if (callback == null) return;

            // TryGetValue: never creates a new Broker when unsubscribing.
            if (!BrokersGeneric.TryGetValue(typeof(T), out var broker)) return;

            var b = (Broker<T>)broker;
            b.Unsubscribe(callback);

            // Auto-cleanup: remove empty broker to allow GC collection.
            if (b.SubscriberCount == 0)
                BrokersGeneric.TryRemove(typeof(T), out _);
        }

        // ── Cleanup ──────────────────────────────────────────────────────────────

        public static void Cleanup<T>()
        {
            if (BrokersGeneric.TryRemove(typeof(T), out var broker) && broker is IDisposable d)
                d.Dispose();
        }

        public static void ClearAll()
        {
            foreach (var broker in BrokersGeneric.Values)
            {
                if (broker is IDisposable d) d.Dispose();
            }
            BrokersGeneric.Clear();
        }
    }
}
