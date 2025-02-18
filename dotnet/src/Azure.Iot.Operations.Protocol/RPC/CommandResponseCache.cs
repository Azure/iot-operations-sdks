// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.RPC
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    internal class CommandResponseCache : ICommandResponseCache
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private static readonly TimeSpan MaxWaitDuration = TimeSpan.FromHours(1);

        private static readonly CommandResponseCache instance;

        internal static IWallClock WallClock = new WallClock();

        private readonly SemaphoreSlim _semaphore;
        private readonly AutoResetEvent _expireEvent;

        private Task _expiryTask;
        private bool _isMaintenanceActive;

        private int _aggregateStorageSize;
        private readonly Dictionary<FullCorrelationId, RequestResponse> _requestResponseCache;
        private readonly PriorityQueue<FullCorrelationId, double> _costBenefitQueue; // may refer to entries that have already been removed via expiry
        private readonly PriorityQueue<FullCorrelationId, DateTime> _dedupQueue; // may refer to entries that have already been removed via eviction

        static CommandResponseCache()
        {
            instance = new CommandResponseCache();
        }

        public static ICommandResponseCache GetCache()
        {
            return instance;
        }

        public CommandResponseCache()
        {
            _semaphore = new SemaphoreSlim(1);
            _expireEvent = new AutoResetEvent(false);
            _expiryTask = Task.CompletedTask;
            _isMaintenanceActive = false;

            _aggregateStorageSize = 0;
            _requestResponseCache = [];
            _costBenefitQueue = new();
            _dedupQueue = new();
        }

        public int MaxEntryCount { get; set; } = 10_000;

        public int MaxAggregatePayloadBytes { get; set; } = 10_000_000;

        public int UnitStorageOverheadBytes { get; set; } = 100;

        public int FixedProcessingOverheadMillis { get; set; } = 10;

        public virtual double CostWeightedBenefit(byte[]? requestPayload, MqttApplicationMessage responseMessage, TimeSpan executionDuration)
        {
            double executionBypassBenefit = FixedProcessingOverheadMillis + executionDuration.TotalMilliseconds;
            double storageCost = UnitStorageOverheadBytes + (requestPayload?.Length ?? 0) + (responseMessage.PayloadSegment.Array?.Length ?? 0);
            return executionBypassBenefit / storageCost;
        }

        public async Task StoreAsync(string commandName, string invokerId, string responseTopic, byte[] correlationData, byte[]? requestPayload, MqttApplicationMessage responseMessage, bool isIdempotent, DateTime commandExpirationTime, TimeSpan executionDuration)
        {
            if (!_isMaintenanceActive)
            {
                throw new AkriMqttException($"{nameof(StoreAsync)} called before {nameof(StartAsync)} or after {nameof(StopAsync)}.")
                {
                    Kind = AkriMqttErrorKind.StateInvalid,
                    InApplication = false,
                    IsShallow = true,
                    IsRemote = false,
                    PropertyName = nameof(this.StoreAsync),
                };
            }

            FullCorrelationId fullCorrelationId = new(responseTopic, correlationData);

            await _semaphore.WaitAsync().ConfigureAwait(false);

            if (!_requestResponseCache.TryGetValue(fullCorrelationId, out RequestResponse? requestResponse))
            {
                _semaphore.Release();
                return;
            }

            requestResponse.Response.SetResult(responseMessage);
            _aggregateStorageSize += requestResponse.Size;

            DateTime now = WallClock.UtcNow;
            bool hasExpired = now >= commandExpirationTime;

            if (hasExpired)
            {
                RemoveEntry(fullCorrelationId, requestResponse);
                _semaphore.Release();
                return;
            }

            bool isDedupMandatory = !isIdempotent;
            double dedupBenefit = CostWeightedBenefit(null, responseMessage, executionDuration);
            double reuseBenefit = CostWeightedBenefit(requestPayload, responseMessage, executionDuration);

            double effectiveBenefit = dedupBenefit;
            bool canEvict = !isDedupMandatory || hasExpired;

            double deferredBenefit = effectiveBenefit;

            _dedupQueue.Enqueue(fullCorrelationId, commandExpirationTime);

            if (canEvict)
            {
                _costBenefitQueue.Enqueue(fullCorrelationId, effectiveBenefit);
                deferredBenefit = 0;
            }

            requestResponse.DeferredBenefit = deferredBenefit;

            TrimCache();

            _semaphore.Release();

            _expireEvent.Set();
        }

        public async Task<Task<MqttApplicationMessage>?> RetrieveAsync(string commandName, string invokerId, string responseTopic, byte[] correlationData, byte[] requestPayload, bool isCacheable, bool canReuseAcrossInvokers)
        {
            Task<MqttApplicationMessage>? responseTask = null;
            await _semaphore.WaitAsync().ConfigureAwait(false);

            FullCorrelationId fullCorrelationId = new(responseTopic, correlationData);
            FullRequest? fullRequest = isCacheable ? new FullRequest(commandName, canReuseAcrossInvokers ? string.Empty : invokerId, requestPayload) : null;

            if (_requestResponseCache.TryGetValue(fullCorrelationId, out RequestResponse? dedupRequestResponse))
            {
                responseTask = dedupRequestResponse.Response.Task;
            }
            else
            {
                _requestResponseCache[fullCorrelationId] = new RequestResponse(fullRequest);
            }

            _semaphore.Release();
            return responseTask;
        }

        public async Task StartAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            if (_isMaintenanceActive)
            {
                _semaphore.Release();
                return;
            }

            _isMaintenanceActive = true;
            _expiryTask = Task.Run(ContinuouslyExpireAsync);

            _semaphore.Release();
        }

        public async Task StopAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            if (!_isMaintenanceActive)
            {
                _semaphore.Release();
                return;
            }

            _isMaintenanceActive = false;
            Task expiryTask = this._expiryTask;

            _semaphore.Release();

            _expireEvent.Set();

            await expiryTask.ConfigureAwait(false);
        }

        private void TrimCache()
        {
            while (_requestResponseCache.Count > MaxEntryCount || _aggregateStorageSize > MaxAggregatePayloadBytes)
            {
                if (!_costBenefitQueue.TryDequeue(out FullCorrelationId? extantCorrelationId, out double _))
                {
                    return;
                }

                if (_requestResponseCache.TryGetValue(extantCorrelationId, out RequestResponse? lowValueEntry))
                {
                    RemoveEntry(extantCorrelationId, lowValueEntry);
                }
            }
        }

        private async Task ContinuouslyExpireAsync()
        {
            while (true)
            {
                await _semaphore.WaitAsync().ConfigureAwait(false);
                if (!_isMaintenanceActive)
                {
                    _semaphore.Release();
                    return;
                }

                TimeSpan remainingDuration = _dedupQueue.TryPeek(out FullCorrelationId? extantCorrelationId, out DateTime commandExpirationTime) ? commandExpirationTime - WallClock.UtcNow : TimeSpan.MaxValue;
                if (remainingDuration > MaxWaitDuration)
                {
                    remainingDuration = MaxWaitDuration;
                }

                if (remainingDuration <= TimeSpan.Zero)
                {
                    if (_dedupQueue.Dequeue() != extantCorrelationId)
                    {
                        _semaphore.Release();
                        throw new AkriMqttException("Internal logic error in CommandResponseCache - inconsistent dedupQueue")
                        {
                            Kind = AkriMqttErrorKind.InternalLogicError,
                            InApplication = false,
                            IsShallow = false,
                            IsRemote = false,
                            PropertyName = nameof(FullCorrelationId.CorrelationData),
                        };
                    }

                    if (_requestResponseCache.TryGetValue(extantCorrelationId, out RequestResponse? extantEntry))
                    {
                        RemoveEntry(extantCorrelationId, extantEntry);
                    }

                    _semaphore.Release();
                    continue;
                }

                _semaphore.Release();
                await Task.Run(() => { _expireEvent.WaitOne(remainingDuration); }).ConfigureAwait(false);
            }
        }

        private void RemoveEntry(FullCorrelationId correlationId, RequestResponse requestResponse)
        {
            _aggregateStorageSize -= requestResponse.Size;
            _requestResponseCache.Remove(correlationId);
        }

        private class FullCorrelationId(string responseTopic, byte[] correlationData)
        {
            public string ResponseTopic { get; } = responseTopic;

            public byte[] CorrelationData { get; } = correlationData ?? [];

            public override bool Equals(object? obj)
            {
                return obj != null && obj is FullCorrelationId other
                    && ResponseTopic == other.ResponseTopic && CorrelationData.SequenceEqual(other.CorrelationData);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 0;
                    hash = 131 * hash + ResponseTopic.GetHashCode();
                    hash = 131 * hash + ((IStructuralEquatable)CorrelationData).GetHashCode(EqualityComparer<byte>.Default);
                    return hash;
                }
            }
        }

        private class FullRequest(string commandName, string invokerId, byte[] payload)
        {
            public string CommandName = commandName;

            public string InvokerId = invokerId;

            public byte[] Payload = payload ?? [];

            public override bool Equals(object? obj)
            {
                return obj != null
&& obj is FullRequest other
&& CommandName == other.CommandName && InvokerId == other.InvokerId && Payload.SequenceEqual(other.Payload);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 0;
                    hash = 131 * hash + CommandName.GetHashCode();
                    hash = 131 * hash + InvokerId.GetHashCode();
                    hash = 131 * hash + ((IStructuralEquatable)Payload).GetHashCode(EqualityComparer<byte>.Default);
                    return hash;
                }
            }

            public int Size => CommandName.Length + InvokerId.Length + Payload.Length;
        }

        private record RequestResponse
        {
            public RequestResponse(FullRequest? fullRequest)
            {
                FullRequest = fullRequest;
                Response = new TaskCompletionSource<MqttApplicationMessage>();
            }

            public FullRequest? FullRequest { get; init; }

            public TaskCompletionSource<MqttApplicationMessage> Response { get; init; }

            public double DeferredBenefit { get; set; }

            public int Size => Response.Task.Status == TaskStatus.RanToCompletion ? (FullRequest?.Size ?? 0) + (Response.Task.Result.PayloadSegment.Array?.Length ?? 0) : 0;
        }

        private record ReuseReference
        {
            public ReuseReference(FullCorrelationId fullCorrelationId)
            {
                FullCorrelationId = fullCorrelationId;
                Ttl = DateTime.MaxValue;
            }

            public FullCorrelationId FullCorrelationId { get; init; }

            public DateTime Ttl { get; set; }
        }
    }
}
