using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PirexMessage
{
    public partial class Broker<T>
    {
        public bool PublishParallel(T data, ParallelOptions parallelOptions)
        {
            if (data == null)
                return false;

            if (parallelOptions == null)
                parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            try
            {
                var isHaveSubscriber = false;
                Parallel.ForEach(_set.Keys, parallelOptions, subscriber =>
                {
                    try
                    {
                        subscriber?.Invoke(data);
                        isHaveSubscriber = true;
                    }
                    catch (Exception ex)
                    {
                        // Remove the problematic subscriber
                        if (subscriber != null) _set.TryRemove(subscriber, out _);
                    }
                });

                return isHaveSubscriber;
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in parallel publishing: {ex.Message}");
                return false;
            }
        }

        public bool PublishParallel(T data)
        {
            return PublishParallel(data, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });
        }
    }
}