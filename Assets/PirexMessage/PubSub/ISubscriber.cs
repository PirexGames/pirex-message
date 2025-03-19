using System;

namespace PirexMessage
{
    public interface ISubscriber<out T>
    {
        IDisposable Subscribe(Action<T> callback);
        void Unsubscribe(Action<T> callback);
    }
}