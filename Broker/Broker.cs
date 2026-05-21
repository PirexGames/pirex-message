using System;
using System.Runtime.CompilerServices;
using System.Threading;
#if PIREX_PIPE_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace PirexMessage
{
    /// <summary>
    /// Thread-safe, near-zero-alloc message broker with two dispatch modes.
    ///
    /// Unordered (default):
    ///   - Subscribe:   O(n) dedup + O(1) append. Alloc only when growing capacity.
    ///   - Unsubscribe: O(n) search + O(1) swap-remove. ZERO alloc.
    ///   - Publish:     O(n) iterate [0.._count). ZERO alloc, ZERO lock.
    ///
    /// Ordered (preserves insertion order):
    ///   - Subscribe:   O(n) dedup + O(1) append. Alloc only when growing capacity.
    ///   - Unsubscribe: O(n) search + O(n) shift-remove. ZERO alloc.
    ///   - Publish:     O(n) iterate [0.._count). ZERO alloc, ZERO lock.
    ///
    /// After warmup (capacity stabilised + Subscription pool filled), Subscribe/Unsubscribe
    /// also produce ZERO GC.
    /// </summary>
    public sealed partial class Broker<T> : IPublisher<T>, ISubscriber<T>, IDisposable
    {
        private const int InitialCapacity = 4;

        // ── State ─────────────────────────────────────────────────────────────────
        // volatile ref: Publish reads without lock; writers swap under SpinLock.
        private volatile Action<T>[] _slots;
        // volatile int: Publish reads without lock; writers update under SpinLock.
        private volatile int _count;

        private readonly bool _ordered;

        // SpinLock: struct (no heap alloc), ideal for short critical sections.
        // Must NOT be readonly: Enter mutates the struct.
        private SpinLock _writeLock = new SpinLock(enableThreadOwnerTracking: false);

        // Cached delegate: avoids one alloc per Subscribe call.
        private readonly Action<Action<T>> _unsubscribeDelegate;

        // volatile: read in Publish (no lock) must see the value written under SpinLock.
        private volatile bool _disposed;

        // ── Constructor ───────────────────────────────────────────────────────────
        /// <param name="ordered">
        /// false (default): subscribers are dispatched in unspecified order (swap-remove).
        /// true:            subscribers are dispatched in subscription order (shift-remove).
        /// </param>
        public Broker(bool ordered = false)
        {
            _ordered             = ordered;
            _slots               = Array.Empty<Action<T>>();
            _unsubscribeDelegate = Unsubscribe;
        }

        // ── ISubscriber ───────────────────────────────────────────────────────────
        public IDisposable Subscribe(Action<T> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            bool lockTaken = false;
            try
            {
                _writeLock.Enter(ref lockTaken);
                if (_disposed) throw new ObjectDisposedException(nameof(Broker<T>));

                // Dedup: scan current live range.
                int n = _count;
                var arr = _slots;
                for (int i = 0; i < n; i++)
                    if (arr[i] == callback) return Subscription<T>.Rent(callback, _unsubscribeDelegate);

                // Grow capacity if needed (amortised O(1) — happens logarithmically).
                if (n == arr.Length)
                {
                    int cap = arr.Length == 0 ? InitialCapacity : arr.Length * 2;
                    var next = new Action<T>[cap];
                    if (n > 0) Array.Copy(arr, next, n);
                    _slots = next;   // volatile write visible to Publish before _count update
                    arr = next;
                }

                arr[n] = callback;
                _count = n + 1;     // volatile write: Publish sees new element atomically
            }
            finally
            {
                if (lockTaken) _writeLock.Exit(); // release fence: ensures all writes above visible
            }

            return Subscription<T>.Rent(callback, _unsubscribeDelegate);
        }

        public void Unsubscribe(Action<T> callback)
        {
            if (callback == null) return;

            bool lockTaken = false;
            try
            {
                _writeLock.Enter(ref lockTaken);
                if (_disposed) return;

                var arr = _slots;
                int n   = _count;
                int idx = IndexOf(arr, callback, n);
                if (idx < 0) return;

                if (_ordered)
                    RemoveOrdered(arr, idx, n);
                else
                    RemoveUnordered(arr, idx, n);
            }
            finally
            {
                if (lockTaken) _writeLock.Exit();
            }
        }

        // Swap the removed element with the last → O(1), no array copy.
        // Changes iteration order — acceptable for unordered mode.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveUnordered(Action<T>[] arr, int idx, int n)
        {
            arr[idx]     = arr[n - 1]; // move last to removed slot
            arr[n - 1]   = null;       // clear last slot (help GC)
            _count       = n - 1;      // volatile write: Publish won't iterate past this
        }

        // Shift elements left → O(n), preserves insertion order.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveOrdered(Action<T>[] arr, int idx, int n)
        {
            // Array.Copy is optimised memmove — faster than a manual loop.
            if (idx < n - 1) Array.Copy(arr, idx + 1, arr, idx, n - idx - 1);
            arr[n - 1] = null;  // clear vacated last slot (help GC)
            _count     = n - 1; // volatile write
        }

        // ── IPublisher ────────────────────────────────────────────────────────────
        /// <summary>
        /// Hot path: ZERO allocation, ZERO lock.
        /// Reads a volatile snapshot of (slots, count) then iterates.
        /// </summary>
        public bool Publish(T data)
        {
            var arr = _slots;  // volatile read — acquire fence
            int n   = _count;  // volatile read

            if (_disposed) throw new ObjectDisposedException(nameof(Broker<T>));
            if (n == 0) return false;

            for (int i = 0; i < n; i++)
            {
                var s = arr[i];
                if (s == null) continue; // safety: concurrent unordered write
                try   { s(data); }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                    Unsubscribe(s); // rare — exception in subscriber
                }
            }

            return true;
        }

#if PIREX_PIPE_UNITASK
        public UniTask<bool> PublishAsync(T data)
        {
            return UniTask.FromResult(Publish(data));
        }
#endif

        // ── Misc ──────────────────────────────────────────────────────────────────
        public int SubscriberCount => _count;

        public void Dispose()
        {
            bool lockTaken = false;
            try
            {
                _writeLock.Enter(ref lockTaken);
                if (_disposed) return;
                _disposed = true;
                // Clear all slot refs so GC can collect delegate/closure objects.
                var arr = _slots;
                int n   = _count;
                for (int i = 0; i < n; i++) arr[i] = null;
                _count = 0;
            }
            finally
            {
                if (lockTaken) _writeLock.Exit();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int IndexOf(Action<T>[] arr, Action<T> target, int count)
        {
            for (int i = 0; i < count; i++)
                if (arr[i] == target) return i;
            return -1;
        }
    }
}
