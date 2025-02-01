// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public class EventDrivenWorker : BackgroundService
    {
        private SemaphoreSlim _assetSemaphore = new(1);
        Dictionary<string, Asset> _sampleableAssets = new Dictionary<string, Asset>();
        private EventDrivenTelemetryConnectorWorker _connector;
        private ILogger<EventDrivenTelemetryConnectorWorker> _logger;

        public EventDrivenWorker(ILogger<EventDrivenTelemetryConnectorWorker> logger, EventDrivenTelemetryConnectorWorker connectorWorker)
        {
            _logger = logger;
            _connector = connectorWorker;
            _connector.OnAssetSampleable += OnAssetSampleable;
            _connector.OnAssetNotSampleable += OnAssetNotSampleable;
        }

        public void OnAssetNotSampleable(object? sender, AssetUnavailabileEventArgs args)
        {
            _assetSemaphore.Wait();
            try
            {
                if (_sampleableAssets.Remove(args.AssetName, out Asset? asset))
                {
                    _logger.LogInformation("Asset with name {0} is no longer sampleable", asset.DisplayName);
                }
            }
            finally
            {
                _assetSemaphore.Release();
            }
        }

        public void OnAssetSampleable(object? sender, AssetAvailabileEventArgs args)
        {
            _assetSemaphore.Wait();
            try
            {
                if (_sampleableAssets.TryAdd(args.AssetName, args.Asset))
                {
                    _logger.LogInformation("Asset with name {0} is now sampleable", args.AssetName);
                }
            }
            finally
            {
                _assetSemaphore.Release();
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}