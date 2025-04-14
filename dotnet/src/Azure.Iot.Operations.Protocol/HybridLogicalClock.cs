// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol
{
    // based largely on
    // https://jaredforsyth.com/posts/hybrid-logical-clocks/
    // and
    // https://github.com/CharlieTap/hlc/blob/main/src/commonMain/kotlin/com/tap/hlc/HybridLogicalClock.kt
    public class HybridLogicalClock : IAsyncDisposable
    {
        private readonly TimeSpan _defaultMaxClockDrift = TimeSpan.FromMinutes(1);
        private readonly SemaphoreSlim _semaphoreHlc = new SemaphoreSlim(1, 1);
        private bool _disposed;

        // The base to use when reading an int while encoding/decoding the HLC
        private const int _encodingBase = 10;

        private DateTime _timestamp;

        /// <summary>
        /// The current timestamp for this hybrid logical clock.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value is used in conjunction with <see cref="Counter"/> to coordinate ordering of events within
        /// a distributed system where each device may have slightly different system clock times.
        /// </para>
        /// <para>
        /// This timestamp only allows for millisecond-level precision. Any provided value will automatically
        /// be rounded down to the nearest millisecond.
        /// </para>
        /// </remarks>
        public DateTime Timestamp
        {
            get => _timestamp;

            // take the floor of the total milliseconds to avoid sub-millisecond precision
            set => _timestamp = FloorToMillisecondPrecision(value);
        }

        /// <summary>
        /// The counter for this hybrid logical clock.
        /// </summary>
        /// <remarks>
        /// This counter is used in conjunction with the <see cref="Timestamp"/> to coordinate ordering of
        /// events within a distributed system where each device may have slightly different system clock times.
        /// </remarks>
        public int Counter { get; set; }

        /// <summary>
        /// A unique identifier for this node.
        /// </summary>
        /// <remarks>
        /// This is only used to break ties where both the counter and timestamp of
        /// two competing nodes are exactly the same.
        /// </remarks>
        public string NodeId { get; }

        /// <summary>
        /// Construct a HybridLogicalClock instance.
        /// </summary>
        /// <param name="timestamp">The timestamp for this clock. This timestamp will be rounded down to the nearest millisecond.</param>
        /// <param name="counter">The counter for this clock.</param>
        /// <param name="nodeId">The node identifier for this clock.</param>
        public HybridLogicalClock(DateTime? timestamp = null, int counter = 0, string? nodeId = null)
        {
            Timestamp = timestamp == null ? DateTime.UtcNow : timestamp.Value;

            Counter = counter;

            NodeId = string.IsNullOrWhiteSpace(nodeId) ? Guid.NewGuid().ToString() : nodeId;
        }

        /// <summary>
        /// Copy construct a HybridLogicalClock from another HybridLogicalClock
        /// </summary>
        /// <param name="other"></param>
        public HybridLogicalClock(HybridLogicalClock other)
            : this(other.Timestamp, other.Counter, other.NodeId)
        {
        }

        /// <summary>
        /// Updates the application's HybridLogicalClock based on the current time and returns its string representation.
        /// </summary>
        /// <param name="maxClockDrift">Maximum allowed clock drift. Defaults to 1 minute if not specified.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>String representation of the updated HLC.</returns>
        /// <exception cref="ObjectDisposedException">If this instance has been disposed.</exception>
        /// <exception cref="OperationCanceledException">If the operation is canceled.</exception>
        public async Task<string> UpdateNowAsync(TimeSpan? maxClockDrift = null, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            await _semaphoreHlc.WaitAsync(cancellationToken);
            try
            {
                Update(maxClockDrift: maxClockDrift);
                return EncodeToString();
            }
            finally
            {
                _semaphoreHlc.Release();
            }
        }

        /// <summary>
        /// Updates the application's HybridLogicalClock based on another HybridLogicalClock.
        /// </summary>
        /// <param name="other">The other HLC to update against.</param>
        /// <param name="maxClockDrift">Maximum allowed clock drift. Defaults to 1 minute if not specified.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">If this instance has been disposed.</exception>
        /// <exception cref="OperationCanceledException">If the operation is canceled.</exception>
        public async Task UpdateWithOtherAsync(HybridLogicalClock other, TimeSpan? maxClockDrift = null, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            await _semaphoreHlc.WaitAsync(cancellationToken);
            try
            {
                Update(other, maxClockDrift);
            }
            finally
            {
                _semaphoreHlc.Release();
            }
        }

        /// <summary>
        /// Update this clock with details provided by another clock.
        /// </summary>
        /// <param name="other">The other clock.</param>
        /// <param name="maxClockDrift">The maximum allowed clock drift.</param>
        /// <exception cref="AkriMqttException">If the other clock has the same node Id, if
        /// the counter on this clock overflows, or if clock skew that exceeds the provided 
        /// <paramref name="maxClockDrift"/> is detected.</exception>
        private void Update(HybridLogicalClock? other = null, TimeSpan? maxClockDrift = null)
        {
            bool isLocalUpdate = false;
            if (other == null)
            {
                // Update this hybrid logical clock from the wall clock.
                other = new HybridLogicalClock();
                isLocalUpdate = true; // a local update preceding IO
            }

            DateTime wallClockTime = DateTime.UtcNow;
            Validate(wallClockTime, maxClockDrift);

            if (NodeId.Equals(other.NodeId, StringComparison.Ordinal))
            {
                // Do not update from self
                return;
            }

            if (maxClockDrift == null)
            {
                maxClockDrift = _defaultMaxClockDrift;
            }

            if (wallClockTime.CompareTo(Timestamp) > 0
                && wallClockTime.CompareTo(other.Timestamp) > 0)
            {
                Timestamp = wallClockTime;
                Counter = 0;
            }
            else if (Timestamp.CompareTo(other.Timestamp) == 0)
            {
                int maxCounter = Math.Max(Counter, other.Counter);

                // The counter may overflow from this step, so check before assigning anything
                if (maxCounter == int.MaxValue)
                {
                    throw new AkriMqttException($"Integer overflow on HybridLogicalClock counter. {nameof(maxCounter)} = {int.MaxValue}")
                    {
                        Kind = AkriMqttErrorKind.InternalLogicError,
                        IsShallow = isLocalUpdate,
                        IsRemote = false,
                    };
                }

                Counter = maxCounter + 1;
            }
            else if (Timestamp.CompareTo(other.Timestamp) > 0)
            {
                Counter += 1;
            }
            else
            {
                Timestamp = other.Timestamp;
                Counter = other.Counter + 1;
            }

            Validate(wallClockTime, maxClockDrift);
        }

        public int CompareTo(HybridLogicalClock other)
        {
            if (Timestamp.CompareTo(other.Timestamp) == 0)
            {
                if (Counter == other.Counter)
                {
                    if (NodeId.Equals(other.NodeId, StringComparison.Ordinal))
                    {
                        // Should never happen. Each node must have a unique ID
                        return 0;
                    }

                    // This is a tie-breaker scenario that usually doesn't need to be checked
                    return string.CompareOrdinal(NodeId, other.NodeId);
                }

                return Counter - other.Counter;
            }

            return Timestamp.CompareTo(other.Timestamp);
        }

        public override bool Equals(object? obj)
        {
            return obj != null
                    && obj is HybridLogicalClock otherHlc
                    && NodeId.Equals(otherHlc.NodeId, StringComparison.Ordinal)
                    && Counter == otherHlc.Counter
                    && Timestamp.Equals(otherHlc.Timestamp);
        }

        public string EncodeToString()
        {
            double millisecondsSinceUnixEpoch = (Timestamp - DateTime.UnixEpoch).TotalMilliseconds;
            return $"{millisecondsSinceUnixEpoch.ToString(CultureInfo.InvariantCulture).PadLeft(15, '0')}:{Convert.ToString(Counter, _encodingBase).PadLeft(5, '0')}:{NodeId}";
        }

        public static HybridLogicalClock DecodeFromString(string propertyName, string encoded)
        {
            string[] parts = encoded.Split(":");

            if (parts.Length != 3)
            {
                throw new AkriMqttException("Malformed HLC. Expected three segments separated by ':' character")
                {
                    Kind = AkriMqttErrorKind.HeaderInvalid,
                    IsShallow = false,
                    IsRemote = false,
                    HeaderName = propertyName,
                    HeaderValue = encoded,
                };
            }

            DateTime timestamp = DateTime.UnixEpoch;
            timestamp = double.TryParse(parts[0], CultureInfo.InvariantCulture, out double result)
                ? timestamp.AddMilliseconds(result)
                : throw new AkriMqttException("Malformed HLC. Could not parse first segment as an integer")
                {
                    Kind = AkriMqttErrorKind.HeaderInvalid,
                    IsShallow = false,
                    IsRemote = false,
                    HeaderName = propertyName,
                    HeaderValue = encoded,
                };

            int counter;
            try
            {
                counter = Convert.ToInt32(parts[1], _encodingBase);
            }
            catch (Exception)
            {
                throw new AkriMqttException("Malformed HLC. Could not parse second segment as a base 32 integer")
                {
                    Kind = AkriMqttErrorKind.HeaderInvalid,
                    IsShallow = false,
                    IsRemote = false,
                    HeaderName = propertyName,
                    HeaderValue = encoded,
                };
            }

            if (parts[2].Length < 1)
            {
                throw new AkriMqttException("Malformed HLC. Missing nodeId as the final segment")
                {
                    Kind = AkriMqttErrorKind.HeaderInvalid,
                    IsShallow = false,
                    IsRemote = false,
                    HeaderName = propertyName,
                    HeaderValue = encoded,
                };
            }

            string nodeId = parts[2];

            return new HybridLogicalClock(timestamp, counter, nodeId);
        }

        private void Validate(DateTime now, TimeSpan? maxClockDrift)
        {
            if (Counter == int.MaxValue)
            {
                throw new AkriMqttException($"Integer overflow on HybridLogicalClock counter. {nameof(Counter)} = {int.MaxValue}")
                {
                    Kind = AkriMqttErrorKind.InternalLogicError,
                    IsShallow = true,
                    IsRemote = false,
                    PropertyName = nameof(Counter),
                };
            }

            if (Timestamp.Subtract(now).Duration() > maxClockDrift)
            {
                throw new AkriMqttException("Clock drift")
                {
                    Kind = AkriMqttErrorKind.StateInvalid,
                    IsShallow = true,
                    IsRemote = false,
                    PropertyName = "MaxClockDrift",
                };
            }
        }

        internal static DateTime FloorToMillisecondPrecision(DateTime dt)
        {
            return dt.AddTicks(-1 * (dt.Ticks % TimeSpan.FromMilliseconds(1).Ticks));
        }

        public override int GetHashCode()
        {
            return NodeId.GetHashCode() + Timestamp.GetHashCode() + Counter.GetHashCode();
        }

        public override string ToString()
        {
            return EncodeToString();
        }

        /// <summary>
        /// Asynchronously releases the resources used by the ApplicationContext.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (!_disposed)
            {
                _semaphoreHlc?.Dispose();
                _disposed = true;
            }

            await ValueTask.CompletedTask;
        }
    }
}
