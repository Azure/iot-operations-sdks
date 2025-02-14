// Copyright (c) Microsoft Corporation.
//  Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Azure.Iot.Operations.Mqtt
{
    /// <summary>
    /// A blocking queue that is thread safe and that allows for each element to specify when it is "ready" to be dequeued.
    /// </summary>
    /// <remarks>
    /// Items in this queue may be marked as ready to be dequeued in any order, but the blocking calls 
    /// <see cref="Dequeue(CancellationToken)"/> and <see cref="Peek(CancellationToken)"/> will only ever return 
    /// if the head of the queue is ready.
    /// </remarks>
    /// <typeparam name="T">The type of all the elements in the blocking queue.</typeparam>
    internal class BlockingConcurrentDelayableQueue<T> : IDisposable
        where T : IDelayableQueueItem
    {
        ConcurrentQueue<T> _queue;
        ManualResetEventSlim _gate;

        public BlockingConcurrentDelayableQueue()
        {
            _queue = new ConcurrentQueue<T>();
            _gate = new ManualResetEventSlim(false);
        }

        /// <summary>
        /// Delete all entries from this queue.
        /// </summary>
        public void Clear()
        {
            _queue.Clear();
        }

        public int Count => _queue.Count;

        /// <summary>
        /// Enqueue the provided item.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        public void Enqueue(T item)
        {
            _queue.Enqueue(item);
            _gate.Set();
        }

        /// <summary>
        /// Block until there is a first element in the queue and that element is ready to be dequeued then dequeue and
        /// return that element.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The first element in the queue.</returns>
        public T Dequeue(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (_queue.IsEmpty)
                {
                    _gate.Reset();
                    _gate.Wait(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    continue;
                }
                else
                {
                    if (_queue.TryPeek(out T? item)
                        && item.IsReady()
                        && _queue.TryDequeue(out T? dequeuedItem))
                    {
                        return dequeuedItem;
                    }
                    else
                    {
                        _gate.Reset();
                        _gate.Wait(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Wakeup any blocking calls not because a new element was added to the queue, but because
        /// one or more elements in the queue is now ready.
        /// </summary>
        /// <remarks>
        /// Generally, this method should be called every time an item in this queue is marked as ready.
        /// </remarks>
        public void Signal()
        {
            _gate.Set();
        }

        public void Dispose()
        {
            _gate.Dispose();
        }
    }
}