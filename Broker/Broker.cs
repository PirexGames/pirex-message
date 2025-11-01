using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
#if PIREX_PIPE_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace PirexMessage
{
    public sealed partial class Broker<T> : IPublisher<T>, ISubscriber<T>
    {
        private readonly ConcurrentDictionary<Action<T>, byte> _set = new ConcurrentDictionary<Action<T>, byte>();
        private Action<T>[] _snapshot = Array.Empty<Action<T>>();
        private int _version;
        private int _snapVersion;
        private readonly object _rebuildLock = new object();

        public IDisposable Subscribe(Action<T> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            _set.TryAdd(callback, 0);
            Interlocked.Increment(ref _version);
            return new Subscription<T>(callback, Unsubscribe);
        }

        public void Unsubscribe(Action<T> callback)
        {
            if (callback == null) return;
            _set.TryRemove(callback, out _);
            Interlocked.Increment(ref _version);
        }

        public bool Publish(T data)
        {
            if (data == null) return false;

            var ver = Volatile.Read(ref _version);
            if (ver != Volatile.Read(ref _snapVersion))
            {
                lock (_rebuildLock)
                {
                    ver = Volatile.Read(ref _version);
                    if (ver != _snapVersion)
                    {
                        var arrSnapShot = new Action<T>[_set.Count];
                        int i = 0;
                        foreach (var kv in _set) arrSnapShot[i++] = kv.Key;
                        Volatile.Write(ref _snapshot, arrSnapShot);
                        Volatile.Write(ref _snapVersion, ver);
                    }
                }
            }

            var arr = Volatile.Read(ref _snapshot);
            List<Action<T>> faulty = null;

            for (int i = 0; i < arr.Length; i++)
            {
                try { arr[i]?.Invoke(data); }
                catch { (faulty ??= new List<Action<T>>(4)).Add(arr[i]); }
            }

            if (faulty != null)
            {
                for (int i = 0; i < faulty.Count; i++)
                    _set.TryRemove(faulty[i], out _);
                Interlocked.Add(ref _version, faulty.Count);
            }

            return arr.Length > 0;
        }

#if PIREX_PIPE_UNITASK
        public UniTask<bool> PublishAsync(T data)
        {
            var ok = Publish(data);
            return UniTask.FromResult(ok);
        }
#endif

        public int SubscriberCount => _set.Count;
    }
}
