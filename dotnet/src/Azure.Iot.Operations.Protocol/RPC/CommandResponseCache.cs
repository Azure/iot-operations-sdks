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

        private static CommandResponseCache instance;

        internal static IWallClock WallClock = new WallClock();

        private SemaphoreSlim semaphore;
        private AutoResetEvent expireEvent;
        private AutoResetEvent refreshEvent;

        private Task expiryTask;
        private Task refreshTask;
        private bool isMaintenanceActive;

        private int aggregateStorageSize;
        private Dictionary<FullCorrelationId, RequestResponse> requestResponseCache;
        private Dictionary<FullRequest, ReuseReference> reuseReferenceMap;
        private PriorityQueue<FullCorrelationId, double> costBenefitQueue; // may refer to entries that have already been removed via expiry or refresh
        private PriorityQueue<FullCorrelationId, DateTime> dedupQueue; // may refer to entries that have already been removed via refresh or eviction
        private PriorityQueue<FullCorrelationId, DateTime> reuseQueue; // may refer to entries that have already been removed via expiry or eviction

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
            refreshEvent = new AutoResetEvent(false);
            expiryTask = Task.CompletedTask;
            refreshTask = Task.CompletedTask;
            isMaintenanceActive = false;

            aggregateStorageSize = 0;
            requestResponseCache = new();
            reuseReferenceMap = new();
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

        public async Task StoreAsync(string commandName, string invokerId, byte[] correlationData, byte[]? requestPayload, MqttApplicationMessage responseMessage, bool isIdempotent, DateTime commandExpirationTime, DateTime ttl, TimeSpan executionDuration)
        {
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

            FullCorrelationId fullCorrelationId = new FullCorrelationId(invokerId, correlationData);

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
            refreshEvent.Set();
        }

        public async Task<Task<MqttApplicationMessage>?> RetrieveAsync(string commandName, string invokerId, byte[] correlationData, byte[] requestPayload, bool isCacheable, bool canReuseAcrossInvokers)
        {
            Task<MqttApplicationMessage>? responseTask = null;
            await semaphore.WaitAsync().ConfigureAwait(false);

            FullCorrelationId fullCorrelationId = new FullCorrelationId(invokerId, correlationData);
            FullRequest? fullRequest = isCacheable ? new FullRequest(commandName, canReuseAcrossInvokers ? string.Empty : invokerId, requestPayload) : null;

            if (requestResponseCache.TryGetValue(fullCorrelationId, out RequestResponse? dedupRequestResponse))
            {
                responseTask = dedupRequestResponse.Response.Task;
            }
            else if (fullRequest != null && reuseReferenceMap.TryGetValue(fullRequest, out ReuseReference? reuseReference) && WallClock.UtcNow < reuseReference.Ttl)
            {
                responseTask = requestResponseCache[reuseReference.FullCorrelationId].Response.Task;
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
            refreshTask = Task.Run(ContinuouslyRefreshAsync);

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
            Task refreshTask = this.refreshTask;

            semaphore.Release();

            expireEvent.Set();
            refreshEvent.Set();

            await expiryTask.ConfigureAwait(false);
            await refreshTask.ConfigureAwait(false);
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

        private async Task ContinuouslyRefreshAsync()
        {
            while (true)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
                if (!isMaintenanceActive)
                {
                    semaphore.Release();
                    return;
                }

                TimeSpan remainingDuration = reuseQueue.TryPeek(out FullCorrelationId? extantCorrelationId, out DateTime ttl) ? ttl - WallClock.UtcNow : TimeSpan.MaxValue;
                if (remainingDuration > MaxWaitDuration)
                {
                    remainingDuration = MaxWaitDuration;
                }

                if (remainingDuration <= TimeSpan.Zero)
                {
                    if (reuseQueue.Dequeue() != extantCorrelationId)
                    {
                        semaphore.Release();
                        throw new AkriMqttException("Internal logic error in CommandResponseCache - inconsistent reuseQueue")
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
                        if (extantEntry.DeferredExpirationTime > WallClock.UtcNow)
                        {
                            dedupQueue.Enqueue(extantCorrelationId, extantEntry.DeferredExpirationTime);
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
                await Task.Run(() => { refreshEvent.WaitOne(remainingDuration); }).ConfigureAwait(false);
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

        private class FullCorrelationId
        {
            public FullCorrelationId(string invokerId, byte[] correlationData)
            {
                InvokerId = invokerId;
                CorrelationData = correlationData ?? Array.Empty<byte>();
            }

            public string InvokerId { get; }

            public byte[] CorrelationData { get; }

            public override bool Equals(object? obj)
            {
                if (obj == null)
                {
                    return false;
                }

                var other = obj as FullCorrelationId;
                if (other == null)
                {
                    return false;
                }

                return InvokerId == other.InvokerId && CorrelationData.SequenceEqual(other.CorrelationData);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 0;
                    hash = 131 * hash + InvokerId.GetHashCode();
                    hash = 131 * hash + ((IStructuralEquatable)CorrelationData).GetHashCode(EqualityComparer<byte>.Default);
                    return hash;
                }
            }
        }

        private class FullRequest
        {
            public string CommandName;

            public string InvokerId;

            public byte[] Payload;

            public FullRequest(string commandName, string invokerId, byte[] payload)
            {
                CommandName = commandName;
                InvokerId = invokerId;
                Payload = payload ?? Array.Empty<byte>();
            }

            public override bool Equals(object? obj)
            {
                if (obj == null)
                {
                    return false;
                }

                var other = obj as FullRequest;
                if (other == null)
                {
                    return false;
                }

                return CommandName == other.CommandName && InvokerId == other.InvokerId && Payload.SequenceEqual(other.Payload);
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

            public int Size { get => CommandName.Length + InvokerId.Length + Payload.Length; }
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

            public int Size { get => Response.Task.Status == TaskStatus.RanToCompletion ? (FullRequest?.Size ?? 0) + (Response.Task.Result.PayloadSegment.Array?.Length ?? 0) : 0; }
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
