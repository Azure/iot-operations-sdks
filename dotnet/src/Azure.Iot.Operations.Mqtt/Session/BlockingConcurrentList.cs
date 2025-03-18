// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session.Exceptions;

namespace Azure.Iot.Operations.Mqtt.Session
{
    internal sealed class BlockingConcurrentList : IDisposable
    {
        // This semaphore is responsible for allowing only one thread to interact with this list at a time.
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1);

        // This semaphore is responsible for tracking how many items are in the list for the purpose of blocking consumer threads
        // until there is at least one item to consume.
        private SemaphoreSlim _gate = new SemaphoreSlim(0);

        private readonly LinkedList<QueuedRequest> _items = new();
        private readonly uint _maxSize;
        private readonly MqttPendingMessagesOverflowStrategy _overflowStrategy;

        public BlockingConcurrentList(uint maxSize, MqttPendingMessagesOverflowStrategy overflowStrategy)
        {
            if (maxSize < 1)
            {
                throw new ArgumentException("Max queue size must be greater than 0");
            }

            _maxSize = maxSize;
            _overflowStrategy = overflowStrategy;
        }

        public int Count => _items.Count;

        /// <summary>
        /// Add the item to the end of the list.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// If the item would overflow the list, then it will either push out the item at the front of the list or will itself be skipped.
        /// </remarks>
        public async Task AddLastAsync(QueuedRequest item, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(nameof(item));

            await _mutex.WaitAsync(cancellationToken);
            try
            {
                if (_items.Count < _maxSize)
                {
                    _items.AddLast(item);
                }
                else
                {
                    if (_overflowStrategy == MqttPendingMessagesOverflowStrategy.DropNewMessage)
                    {
                        item.OnException(new MessagePurgedFromQueueException(_overflowStrategy));
                    }
                    else if (_items.First != null && _overflowStrategy == MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage)
                    {
                        var first = _items.First;
                        _items.RemoveFirst();
                        first.Value.OnException(new MessagePurgedFromQueueException(_overflowStrategy));
                        _items.AddLast(item);
                    }
                }

                _gate.Release();
            }
            finally
            {
                _mutex.Release();
            }
        }

        /// <summary>
        /// Remove the provided item from the list.
        /// </summary>
        /// <param name="item">The item to remove from the list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the list contained the item. False otherwise.</returns>
        /// <remarks>
        /// Items can be removed from any position in the list.
        /// </remarks>
        public async Task<bool> RemoveAsync(QueuedRequest item, CancellationToken cancellationToken)
        {
            await _mutex.WaitAsync(cancellationToken);
            try
            {
                return _items.Remove(item);
            }
            finally
            {
                _mutex.Release();
            }
        }

        /// <summary>
        /// Reset the state of each item in this list so that they can be sent again if necessary.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This should be called whenever a connection is lost so that any sent-but-unacknowledged items can be sent again once
        /// the connection is recovered.
        /// </remarks>
        public async Task MarkMessagesAsUnsent(CancellationToken cancellationToken = default)
        {
            await _mutex.WaitAsync(cancellationToken);
            try
            {
                foreach (var item in _items)
                {
                    if (!item.IsInFlight)
                    {
                        // Items in this list are ordered such that all unsent messages are at the back of the list, so the first
                        // encountered unsent message signals that the remaining messages also have not been sent, so they don't need
                        // to be looked at
                        break;
                    }
                    else
                    {
                        // This item was sent, but not acknowledged, so it should be sent again. Items that were sent and acknowledged
                        // should not be reset as they don't need to be sent again.
                        item.IsInFlight = false;
                        _gate.Release();
                    }
                }
            }
            finally
            {
                _mutex.Release();
            }
        }

        /// <summary>
        /// Peek the front-most item in the list that is not already in flight.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>The front-most item in the list that is not already in flight.</returns>
        /// <remarks>
        /// If no item is currently present, or all items are currently in flight, this call will block until an 
        /// item is either added or the state of an existing item is reset via <see cref="MarkMessagesAsUnsent(CancellationToken)"/>.
        /// </remarks>
        public async Task<QueuedRequest> PeekNextUnsentAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                _gate.Wait(cancellationToken);

                bool gateUsed = false;
                try
                {
                    await _mutex.WaitAsync(cancellationToken);

                    try
                    {
                        var item = _items.First;
                        if (item == null || _items.Count == 0)
                        {
                            continue;
                        }

                        while (item.Value.IsInFlight)
                        {
                            item = item.Next;

                            if (item == null)
                            {
                                break;
                            }
                        }

                        if (item != null)
                        {
                            gateUsed = true;
                            item.Value.IsInFlight = true; // The item should only be peeked when it is about to be sent
                            return item.Value;
                        }
                    }
                    finally
                    {
                        _mutex.Release();
                    }

                }
                finally
                {
                    if (!gateUsed)
                    {
                        _gate.Release(1);
                    }
                }
            }
        }

        public async Task CancelAllRequestsAsync(Exception reason, CancellationToken cancellationToken)
        {
            try
            {
                await _mutex.WaitAsync(cancellationToken);
                foreach (var item in _items)
                {
                    item.OnException(reason);
                }

                _items.Clear();

                // Clear the gate so that it reflects that there are no more queued requests
                _gate.Dispose();
                _gate = new SemaphoreSlim(0);
            }
            finally
            {
                _mutex.Release();
            }
        }
        public void Dispose()
        {
            _gate.Dispose();
            _mutex.Dispose();
        }
    }
}