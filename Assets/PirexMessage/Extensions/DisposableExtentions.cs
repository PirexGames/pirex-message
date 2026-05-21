using System;
using UnityEngine;

namespace PirexMessage
{
    public static class DisposableExtensions
    {
        // Cached static action to avoid closure alloc and boxing when T is a class.
        // For value-type T: constraint "class" below prevents this method from being
        // called with structs — use the overload below instead.

        /// <summary>
        /// Zero-alloc overload for reference-type IDisposable (e.g. Subscription&lt;T&gt;).
        /// No boxing: T is constrained to class, so storing as object is a reference copy.
        /// </summary>
        public static void DisposeOnDestroy<T>(this T disposable, MonoBehaviour component)
            where T : class, IDisposable
        {
            // Register(Action<object>, object): state stored as object.
            // T is a class → no boxing, just a reference cast.
            component.destroyCancellationToken.Register(
                static obj => ((T)obj).Dispose(),   // static lambda: no closure, cached per T
                disposable);
        }
    }
}