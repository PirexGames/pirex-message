using System;

public class Subscription<T> : IDisposable
{
    private readonly Action<T> _callback;
    private readonly Action<Action<T>> _unsubscribe;
    private bool _isDisposed;

    public Subscription(Action<T> callback, Action<Action<T>> unsubscribe)
    {
        _callback = callback;
        _unsubscribe = unsubscribe;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _unsubscribe?.Invoke(_callback);
        _isDisposed = true;
    }
}