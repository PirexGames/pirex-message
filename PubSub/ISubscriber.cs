using System;

namespace PirexMessage
{
    public interface ISubscriber<out T>
    {
        IDisposable Subscribe(Action<T> callback, Predicate<T> filter = null);
        void Unsubscribe(Action<T> callback);
    }
}