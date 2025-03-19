using System;
using System.Collections.Generic;
using UnityEngine;

namespace PirexMessage
{
    public static class DisposableExtensions
    {
        public static void DisposeOnDestroy<T>(this T disposable, MonoBehaviour component) where T : IDisposable
        {
            component.destroyCancellationToken.Register(obj => ((T)obj).Dispose(), disposable);
        }
    }
}