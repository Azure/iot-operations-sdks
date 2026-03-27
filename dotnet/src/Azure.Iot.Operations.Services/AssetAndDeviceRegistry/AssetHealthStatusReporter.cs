// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    // allows user to send individual health events (if they are diff from cached) and also sends a batch of those cached health events periodically regardless.
    // users can still stop the periodic sending of particular events/streams/datasets/etc without preventing the other events/streams/datasets/etc from their
    // periodic batch send. The periodic sending starts as soon as the first entity reports a health status and stops if all cached entries have been paused or
    // if the reporter is disposed.
    public class AssetHealthStatusReporter : IAsyncDisposable
    {
        private readonly Dictionary<string, RuntimeHealth?> _cachedDatasetsRuntimeHealth = new();

        private readonly Countdown? _periodicSender;

        private readonly IAzureDeviceRegistryClient _azureDeviceRegistryClient;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;
        private readonly string _assetName;

        public AssetHealthStatusReporter(IAzureDeviceRegistryClient azureDeviceRegistryClient, string deviceName, string inboundEndpointName, string assetName, TimeSpan? reportingPeriod = null)
        {
            _azureDeviceRegistryClient = azureDeviceRegistryClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
            _assetName = assetName;
            _periodicSender = new Countdown(reportingPeriod ?? TimeSpan.FromSeconds(10), SendAggregateReportAsync);
        }

        private async Task SendAggregateReportAsync(CancellationToken cancellationToken)
        {
            List<DatasetsRuntimeHealthEvent> datasetRuntimeHealthsToReport = new();
            foreach (string datasetName in _cachedDatasetsRuntimeHealth.Keys)
            {
                if (_cachedDatasetsRuntimeHealth.TryGetValue(datasetName, out RuntimeHealth? cachedDatasetHealth) && cachedDatasetHealth != null)
                {
                    datasetRuntimeHealthsToReport.Add(new DatasetsRuntimeHealthEvent()
                    {
                        DatasetName = datasetName,
                        RuntimeHealth = cachedDatasetHealth,
                    });
                }
            }

            if (datasetRuntimeHealthsToReport.Count > 0)
            {
                await _azureDeviceRegistryClient.ReportDatasetRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, datasetRuntimeHealthsToReport, cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Pause background reporting until a new health status is set
        /// </summary>
        /// <remarks>
        /// This is used to signal that the last known health status may no longer be applicable.
        /// </remarks>
        public void PauseReportingDataset(string datasetName)
        {
            _cachedDatasetsRuntimeHealth[datasetName] = null;
        }

        // should be called when the asset is deleted
        public async Task CancelHealthStatusReportingAsync(CancellationToken cancellationToken = default)
        {
            if (_periodicSender != null)
            {
                await _periodicSender.StopAsync(cancellationToken);
            }
        }

        public async Task ReportDatasetHealthStatusAsync(string datasetName, RuntimeHealth datasetRuntimeHealth, TimeSpan? telemetryTimeout = default, CancellationToken cancellationToken = default)
        {
            var datasetHealthEvent = new DatasetsRuntimeHealthEvent()
            {
                DatasetName = datasetName,
                RuntimeHealth = datasetRuntimeHealth,
            };

            _cachedDatasetsRuntimeHealth.TryGetValue(datasetName, out RuntimeHealth? cachedHealth);
            CompareNewHealthWithCachedHealth(datasetRuntimeHealth, cachedHealth, out bool updateCache, out bool sendIt);

            if (updateCache)
            {
                _cachedDatasetsRuntimeHealth[datasetName] = datasetRuntimeHealth;
            }

            if (sendIt)
            { 
                await _azureDeviceRegistryClient.ReportDatasetRuntimeHealthAsync(_deviceName, _inboundEndpointName, _assetName, new List<DatasetsRuntimeHealthEvent> { datasetHealthEvent }, telemetryTimeout, cancellationToken);
            }
        }

        public void CompareNewHealthWithCachedHealth(RuntimeHealth newHealth, RuntimeHealth? cachedHealth, out bool updateCache, out bool sendIt)
        {
            if (cachedHealth == null)
            {
                // This is the first health status event (or the first status event since the user paused reporting), so report it and start periodically reporting
                updateCache = true;
                sendIt = true;
                return;
            }

            if (RuntimeHealth.Equals(newHealth, cachedHealth))
            {
                // The reported health status is no different than the last reported status, so do nothing. This last reported status
                // will be sent by the background reporting later if it doesn't change prior to the next period.
                updateCache = false;
                sendIt = false;
                return;
            }

            if (newHealth.Version < cachedHealth.Version)
            {
                // The reported health status belongs to an older version, so it should not be reported or cached
                updateCache = false;
                sendIt = false;
                return;
            }

            if (RuntimeHealth.EqualsExceptTimestamp(newHealth, cachedHealth) && newHealth.LastUpdateTime.CompareTo(cachedHealth.LastUpdateTime) >= 0)
            {
                // The new health status is identical to the previously sent status, but with a newer timestamp. Just update the timestamp
                // of the cached version so that it is sent on the next background report.
                updateCache = true;
                sendIt = false;
                return;
            }

            // The reported health status is different enough from the last sent status that it should actually be sent to the service and the periodic reporting timer should be restarted
            updateCache = true;
            sendIt = true;
        }

        public virtual async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        public virtual async ValueTask DisposeAsync(bool disposing)
        {
            await DisposeAsyncCore();
        }

        private async ValueTask DisposeAsyncCore()
        {
            try
            {
                if (_periodicSender != null)
                {
                    await _periodicSender.StopAsync();
                    _periodicSender?.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this is already disposed.
            }
        }
    }
}
