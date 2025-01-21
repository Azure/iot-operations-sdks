﻿// Copyright (c) Microsoft Corporation.
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

        private readonly SemaphoreSlim semaphore;
        private readonly AutoResetEvent expireEvent;

        private Task expiryTask;
        private bool isMaintenanceActive;

        private int aggregateStorageSize;
        private readonly Dictionary<FullCorrelationId, RequestResponse> requestResponseCache;
        private readonly Dictionary<FullRequest, ReuseReference> reuseReferenceMap;
        private readonly PriorityQueue<FullCorrelationId, double> costBenefitQueue; // may refer to entries that have already been removed via expiry or refresh
        private readonly PriorityQueue<FullCorrelationId, DateTime> dedupQueue; // may refer to entries that have already been removed via refresh or eviction
        private readonly PriorityQueue<FullCorrelationId, DateTime> reuseQueue; // may refer to entries that have already been removed via expiry or eviction

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
            semaphore = new SemaphoreSlim(1);
            expireEvent = new AutoResetEvent(false);
            expiryTask = Task.CompletedTask;
            isMaintenanceActive = false;

            aggregateStorageSize = 0;
            requestResponseCache = [];
            reuseReferenceMap = [];
            costBenefitQueue = new();
            dedupQueue = new();
            reuseQueue = new();
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

        public async Task StoreAsync(string commandName, string invokerId, string topic, byte[] correlationData, byte[]? requestPayload, MqttApplicationMessage responseMessage, bool isIdempotent, DateTime commandExpirationTime, TimeSpan executionDuration)
        {
            DateTime ttl = DateTime.MinValue;
            if (!isMaintenanceActive)
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

            FullCorrelationId fullCorrelationId = new(topic, correlationData);

            await semaphore.WaitAsync().ConfigureAwait(false);

            if (!requestResponseCache.TryGetValue(fullCorrelationId, out RequestResponse? requestResponse))
            {
                semaphore.Release();
                return;
            }

            requestResponse.Response.SetResult(responseMessage);
            aggregateStorageSize += requestResponse.Size;

            DateTime now = WallClock.UtcNow;
            bool hasExpired = now >= commandExpirationTime;
            bool excessivelyStale = now >= ttl;

            if (hasExpired && excessivelyStale)
            {
                RemoveEntry(fullCorrelationId, requestResponse);
                semaphore.Release();
                return;
            }

            bool isDedupMandatory = !isIdempotent;
            double dedupBenefit = CostWeightedBenefit(null, responseMessage, executionDuration);
            double reuseBenefit = CostWeightedBenefit(requestPayload, responseMessage, executionDuration);

            bool holdForDedup = !hasExpired;
            bool holdForReuse = !excessivelyStale;

            if (!holdForDedup && !holdForReuse)
            {
                RemoveEntry(fullCorrelationId, requestResponse);
                semaphore.Release();
                return;
            }

            double effectiveBenefit = holdForReuse ? reuseBenefit : dedupBenefit;
            bool canEvict = !isDedupMandatory || hasExpired;

            if (requestResponse.FullRequest != null)
            {
                reuseReferenceMap[requestResponse.FullRequest].Ttl = ttl;
            }

            DateTime deferredExpirationTime = holdForDedup ? commandExpirationTime : DateTime.MinValue;
            DateTime deferredStaleness = holdForReuse ? ttl : DateTime.MinValue;
            double deferredBenefit = effectiveBenefit;

            if (holdForDedup && (!holdForReuse || commandExpirationTime < ttl))
            {
                dedupQueue.Enqueue(fullCorrelationId, commandExpirationTime);
                deferredExpirationTime = DateTime.MinValue;
            }
            else
            {
                reuseQueue.Enqueue(fullCorrelationId, ttl);
                deferredStaleness = DateTime.MinValue;
            }

            if (canEvict)
            {
                costBenefitQueue.Enqueue(fullCorrelationId, effectiveBenefit);
                deferredBenefit = 0;
            }

            requestResponse.DeferredExpirationTime = deferredExpirationTime;
            requestResponse.DeferredStaleness = deferredStaleness;
            requestResponse.DeferredBenefit = deferredBenefit;

            TrimCache();

            semaphore.Release();

            expireEvent.Set();
        }

        public async Task<Task<MqttApplicationMessage>?> RetrieveAsync(string commandName, string invokerId, string topic, byte[] correlationData, byte[] requestPayload, bool isCacheable, bool canReuseAcrossInvokers)
        {
            Task<MqttApplicationMessage>? responseTask = null;
            await semaphore.WaitAsync().ConfigureAwait(false);

            FullCorrelationId fullCorrelationId = new(topic, correlationData);
            FullRequest? fullRequest = isCacheable ? new FullRequest(commandName, canReuseAcrossInvokers ? string.Empty : invokerId, requestPayload) : null;

            if (requestResponseCache.TryGetValue(fullCorrelationId, out RequestResponse? dedupRequestResponse))
            {
                responseTask = dedupRequestResponse.Response.Task;
            }
            else
            {
                requestResponseCache[fullCorrelationId] = new RequestResponse(fullRequest);
                if (fullRequest != null)
                {
                    reuseReferenceMap[fullRequest] = new ReuseReference(fullCorrelationId);
                }
            }

            semaphore.Release();
            return responseTask;
        }

        public async Task StartAsync()
        {
            await semaphore.WaitAsync().ConfigureAwait(false);

            if (isMaintenanceActive)
            {
                semaphore.Release();
                return;
            }

            isMaintenanceActive = true;
            expiryTask = Task.Run(ContinuouslyExpireAsync);

            semaphore.Release();
        }

        public async Task StopAsync()
        {
            await semaphore.WaitAsync().ConfigureAwait(false);

            if (!isMaintenanceActive)
            {
                semaphore.Release();
                return;
            }

            isMaintenanceActive = false;
            Task expiryTask = this.expiryTask;

            semaphore.Release();

            expireEvent.Set();

            await expiryTask.ConfigureAwait(false);
        }

        private void TrimCache()
        {
            while (requestResponseCache.Count > MaxEntryCount || aggregateStorageSize > MaxAggregatePayloadBytes)
            {
                if (!costBenefitQueue.TryDequeue(out FullCorrelationId? extantCorrelationId, out double _))
                {
                    return;
                }

                if (requestResponseCache.TryGetValue(extantCorrelationId, out RequestResponse? lowValueEntry))
                {
                    RemoveEntry(extantCorrelationId, lowValueEntry);
                }
            }
        }

        private async Task ContinuouslyExpireAsync()
        {
            while (true)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
                if (!isMaintenanceActive)
                {
                    semaphore.Release();
                    return;
                }

                TimeSpan remainingDuration = dedupQueue.TryPeek(out FullCorrelationId? extantCorrelationId, out DateTime commandExpirationTime) ? commandExpirationTime - WallClock.UtcNow : TimeSpan.MaxValue;
                if (remainingDuration > MaxWaitDuration)
                {
                    remainingDuration = MaxWaitDuration;
                }

                if (remainingDuration <= TimeSpan.Zero)
                {
                    if (dedupQueue.Dequeue() != extantCorrelationId)
                    {
                        semaphore.Release();
                        throw new AkriMqttException("Internal logic error in CommandResponseCache - inconsistent dedupQueue")
                        {
                            Kind = AkriMqttErrorKind.InternalLogicError,
                            InApplication = false,
                            IsShallow = false,
                            IsRemote = false,
                            PropertyName = nameof(FullCorrelationId.CorrelationData),
                        };
                    }

                    if (requestResponseCache.TryGetValue(extantCorrelationId, out RequestResponse? extantEntry))
                    {
                        if (extantEntry.DeferredStaleness > WallClock.UtcNow)
                        {
                            reuseQueue.Enqueue(extantCorrelationId, extantEntry.DeferredStaleness);
                            if (extantEntry.DeferredBenefit != 0)
                            {
                                costBenefitQueue.Enqueue(extantCorrelationId, extantEntry.DeferredBenefit);
                            }

                            TrimCache();
                        }
                        else
                        {
                            RemoveEntry(extantCorrelationId, extantEntry);
                        }
                    }

                    semaphore.Release();
                    continue;
                }

                semaphore.Release();
                await Task.Run(() => { expireEvent.WaitOne(remainingDuration); }).ConfigureAwait(false);
            }
        }

        private void RemoveEntry(FullCorrelationId correlationId, RequestResponse requestResponse)
        {
            aggregateStorageSize -= requestResponse.Size;
            requestResponseCache.Remove(correlationId);
            if (requestResponse.FullRequest != null)
            {
                reuseReferenceMap.Remove(requestResponse.FullRequest);
            }
        }

        private class FullCorrelationId(string topic, byte[] correlationData)
        {
            public string Topic { get; } = topic;

            public byte[] CorrelationData { get; } = correlationData ?? [];

            public override bool Equals(object? obj)
            {
                return obj != null && obj is FullCorrelationId other
                    && Topic == other.Topic && CorrelationData.SequenceEqual(other.CorrelationData);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 0;
                    hash = 131 * hash + Topic.GetHashCode();
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

            public DateTime DeferredExpirationTime { get; set; }

            public DateTime DeferredStaleness { get; set; }

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
