// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using RestThermostatConnector;

namespace Azure.Iot.Operations.Connector
{
    public class EventingWorker : BackgroundService
    {
        private readonly ILogger<EventingWorker> _logger;
        private readonly AssetNotificationHandlerWorker _assetNotificationHandler;

        public EventingWorker(
            ILogger<EventingWorker> logger,
            AssetNotificationHandlerWorker assetNotificationHandler) // todo interface vs impl here
        {
            _logger = logger;
            _assetNotificationHandler = assetNotificationHandler;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            { 
                await Task.Delay(new Random().Next(1000, 5000), cancellationToken);
                await _assetNotificationHandler.SampleAvailableAssetsAsync();
            }
        }
    }
}