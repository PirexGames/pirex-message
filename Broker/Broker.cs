using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PirexMessage
{
    public partial class Broker<T> : IPublisher<T>, ISubscriber<T>
    {
        private readonly ConcurrentDictionary<Action<T>, object> _subscribers = new ConcurrentDictionary<Action<T>, object>();
        private readonly object _lockObject = new object();

        public IDisposable Subscribe(Action<T> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            _subscribers.TryAdd(callback, null);
            return new Subscription<T>(callback, Unsubscribe);
        }

        public void Unsubscribe(Action<T> callback)
        {
            if (callback == null)
                return;

            _subscribers.TryRemove(callback, out _);
        }

        public bool Publish(T data)
        {
            if (data == null)
                return false;

            bool hasSubscribers = false;
            foreach (var subscriber in _subscribers.Keys)
            {
                try
                {
                    subscriber?.Invoke(data);
                    hasSubscribers = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in subscriber callback: {ex.Message}");
                    // Remove the problematic subscriber
                    _subscribers.TryRemove(subscriber, out _);
                }
            }

            return hasSubscribers;
        }

        public async Task<bool> PublishAsync(T data)
        {
            if (data == null)
                return false;

            var tasks = new List<Task>();
            bool hasSubscribers = false;

            foreach (var subscriber in _subscribers.Keys)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        subscriber?.Invoke(data);
                        hasSubscribers = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error in subscriber callback: {ex.Message}");
                        // Remove the problematic subscriber
                        _subscribers.TryRemove(subscriber, out _);
                    }
                }));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }

            return hasSubscribers;
        }

        internal int SubscriberCount => _subscribers.Count;
    }
}