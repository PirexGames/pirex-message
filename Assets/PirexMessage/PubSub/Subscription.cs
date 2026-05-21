using System;
using System.Threading;

namespace PirexMessage
{
    /// <summary>
    /// Pooled disposable subscription token.
    /// After Dispose, the object is returned to a per-type static pool for reuse.
    /// </summary>
    public sealed class Subscription<T> : IDisposable
    {
        // ── Pool ─────────────────────────────────────────────────────────────────
        private const int MaxPoolSize = 32;

        private static readonly Subscription<T>[] Pool = new Subscription<T>[MaxPoolSize];
        private static int _poolCount;
        private static SpinLock _poolLock = new SpinLock(enableThreadOwnerTracking: false);

        internal static Subscription<T> Rent(Action<T> callback, Action<Action<T>> unsubscribe)
        {
            Subscription<T> sub = null;

            bool lockTaken = false;
            try
            {
                _poolLock.Enter(ref lockTaken);
                if (_poolCount > 0)
                    sub = Pool[--_poolCount];
            }
            finally { if (lockTaken) _poolLock.Exit(); }

            if (sub == null) sub = new Subscription<T>();

            sub._callback    = callback;
            sub._unsubscribe = unsubscribe;
            sub._disposed    = 0;
            return sub;
        }

        private static void Return(Subscription<T> sub)
        {
            sub._callback    = null;
            sub._unsubscribe = null;

            bool lockTaken = false;
            try
            {
                _poolLock.Enter(ref lockTaken);
                if (_poolCount < MaxPoolSize)
                    Pool[_poolCount++] = sub;
                // Pool full → let GC collect (rare)
            }
            finally { if (lockTaken) _poolLock.Exit(); }
        }

        // ── Instance ─────────────────────────────────────────────────────────────
        private Action<T>          _callback;
        private Action<Action<T>>  _unsubscribe;
        private int                _disposed;

        private Subscription() { }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _unsubscribe?.Invoke(_callback);
            Return(this);
        }
    }
}