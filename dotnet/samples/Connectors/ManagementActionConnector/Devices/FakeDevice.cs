// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ManagementActionConnector.Devices
{
    /// <summary>
    /// In-process simulator that stands in for a real southbound device. All three
    /// management action handlers (reboot / read-temperature / write-configuration)
    /// share a single instance so writes from one action are observable by reads
    /// on another — the same way they would be on a real device.
    /// </summary>
    /// <remarks>
    /// Exposes a few testability knobs (<see cref="SimulatedLatency"/>,
    /// <see cref="ForceFailure"/>, <see cref="MaxConcurrentOperations"/>,
    /// <see cref="InFlightGate"/>) so tests can deterministically exercise the
    /// SDK's cancellation, drain, exception-translation, and concurrency paths
    /// without depending on real network timing.
    /// </remarks>
    public sealed class FakeDevice
    {
        private readonly object _lock = new();
        private DeviceConfig _config = new(SampleIntervalMs: 1000, Unit: "C");
        private DateTime? _rebootingUntilUtc;
        private long _rebootCounter;

        /// <summary>Optional artificial latency added to every operation.</summary>
        public TimeSpan SimulatedLatency { get; set; } = TimeSpan.Zero;

        /// <summary>If set, the next operation will throw this exception (used to verify SDK exception → RPC error translation).</summary>
        public Exception? ForceFailure { get; set; }

        /// <summary>
        /// Optional gate held by tests to keep an operation in-flight while they
        /// trigger a definition update / asset delete and observe the SDK's drain
        /// behavior.
        /// </summary>
        public TaskCompletionSource? InFlightGate { get; set; }

        /// <summary>Cap on the number of concurrent operations.</summary>
        public int MaxConcurrentOperations
        {
            get => _maxConcurrent;
            set
            {
                _maxConcurrent = value;
                _semaphore = new SemaphoreSlim(value, value);
            }
        }

        private int _maxConcurrent = 8;
        private SemaphoreSlim _semaphore = new(8, 8);

        /// <summary>Snapshot of the current configuration.</summary>
        public DeviceConfig Configuration
        {
            get { lock (_lock) { return _config; } }
        }

        /// <summary>True if the device is currently in the (simulated) reboot window.</summary>
        public bool IsRebooting
        {
            get
            {
                lock (_lock)
                {
                    return _rebootingUntilUtc is { } until && DateTime.UtcNow < until;
                }
            }
        }

        /// <summary>How many reboots have been requested across this device's lifetime.</summary>
        public long RebootCount
        {
            get { lock (_lock) { return _rebootCounter; } }
        }

        /// <summary>Begin a (simulated) reboot. Returns the request id.</summary>
        public async Task<Guid> BeginRebootAsync(bool force, TimeSpan rebootDuration, CancellationToken cancellationToken)
        {
            await EnterAsync(cancellationToken);
            try
            {
                lock (_lock)
                {
                    if (IsRebootingNoLock() && !force)
                    {
                        throw new DeviceBusyException("Device is already rebooting; pass force=true to interrupt.");
                    }

                    _rebootingUntilUtc = DateTime.UtcNow + rebootDuration;
                    _rebootCounter++;
                }

                // Yield so the rebooting flag becomes observable to other in-flight reads.
                await Task.Yield();
                return Guid.NewGuid();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>Read a (simulated) temperature value. Throws if the device is rebooting.</summary>
        public async Task<double> ReadTemperatureAsync(CancellationToken cancellationToken)
        {
            await EnterAsync(cancellationToken);
            try
            {
                if (IsRebooting)
                {
                    throw new DeviceUnavailableException("Device is currently rebooting.");
                }

                // Drift around 22 C (or F, depending on configured unit) by a small sinusoid.
                DeviceConfig cfg;
                lock (_lock) { cfg = _config; }
                double baseline = string.Equals(cfg.Unit, "F", StringComparison.OrdinalIgnoreCase) ? 71.6 : 22.0;
                double drift = Math.Sin(DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond) * 0.5;
                return baseline + drift;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>Apply a configuration update. Returns the new config snapshot.</summary>
        public async Task<DeviceConfig> WriteConfigurationAsync(DeviceConfig newConfig, CancellationToken cancellationToken)
        {
            await EnterAsync(cancellationToken);
            try
            {
                lock (_lock)
                {
                    _config = newConfig;
                    return _config;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task EnterAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (ForceFailure is { } ex)
                {
                    ForceFailure = null;
                    throw ex;
                }

                if (SimulatedLatency > TimeSpan.Zero)
                {
                    await Task.Delay(SimulatedLatency, cancellationToken);
                }

                if (InFlightGate is { } gate)
                {
                    using var reg = cancellationToken.Register(static s => ((TaskCompletionSource)s!).TrySetCanceled(), gate);
                    await gate.Task;
                }
            }
            catch
            {
                _semaphore.Release();
                throw;
            }
            // Successful path: caller must Release().
        }

        private bool IsRebootingNoLock() => _rebootingUntilUtc is { } until && DateTime.UtcNow < until;
    }

    public sealed record DeviceConfig(int SampleIntervalMs, string Unit);

    public sealed class DeviceBusyException : Exception
    {
        public DeviceBusyException(string message) : base(message) { }
    }

    public sealed class DeviceUnavailableException : Exception
    {
        public DeviceUnavailableException(string message) : base(message) { }
    }
}
