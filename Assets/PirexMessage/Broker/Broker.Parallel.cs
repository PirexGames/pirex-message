using System;
using System.Threading.Tasks;
using UnityEngine;

namespace PirexMessage
{
    public partial class Broker<T>
    {
        // Static singleton: avoids `new ParallelOptions` allocation on every no-arg call.
        private static readonly ParallelOptions DefaultParallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        /// <remarks>
        /// WARNING: Runs callbacks on ThreadPool threads.
        /// Do NOT invoke any Unity API (Transform, GameObject, etc.) inside subscribers
        /// when using this overload — Unity's API is not thread-safe.
        ///
        /// Note: Parallel.For requires a closure that captures <paramref name="data"/> and
        /// the snapshot; this path is inherently not zero-alloc (one closure object per call).
        /// Use <see cref="Publish"/> on the main thread for zero-alloc dispatch.
        /// </remarks>
        public bool PublishParallel(T data, ParallelOptions parallelOptions)
        {
            // Volatile reads — same zero-alloc snapshot used by Publish.
            var arr = _slots;
            int n   = _count;
            if (n == 0) return false;

            if (parallelOptions == null) parallelOptions = DefaultParallelOptions;

            try
            {
                Parallel.For(0, n, parallelOptions, i =>
                {
                    var s = arr[i];
                    if (s.Callback == null) return; // safety: null slot possible in ordered mode
                    try   
                    { 
                        if (s.Filter == null || s.Filter(data)) s.Callback(data); 
                    }
                    catch (Exception ex) { Debug.LogException(ex); }
                });
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }

            return true;
        }

        public bool PublishParallel(T data)
        {
            return PublishParallel(data, DefaultParallelOptions);
        }
    }
}