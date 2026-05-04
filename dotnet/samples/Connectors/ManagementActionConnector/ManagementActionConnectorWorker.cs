// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace ManagementActionConnector
{
    /// <summary>
    /// Minimal connector worker demonstrating Management Actions using the simplified
    /// <see cref="IManagementActionHandler"/> interface. The base
    /// <see cref="ManagementActionConnectorWorker"/> handles all executor lifecycle,
    /// notification processing, health reporting, and drain logic internally — this
    /// worker only implements the business logic for each action type.
    /// </summary>
    public sealed class ManagementActionConnectorWorkerSample : BackgroundService
    {
        private readonly ILogger<ManagementActionConnectorWorkerSample> _logger;
        private readonly Azure.Iot.Operations.Connector.ManagementActionConnectorWorker _connector;

        public ManagementActionConnectorWorkerSample(
            ApplicationContext applicationContext,
            ILogger<ManagementActionConnectorWorkerSample> logger,
            ILogger<ConnectorWorker> connectorLogger,
            IMqttClient mqttClient,
            IManagementActionHandlerFactory handlerFactory,
            IMessageSchemaProvider messageSchemaProvider,
            IAzureDeviceRegistryClientWrapperProvider adrClientFactory)
        {
            _logger = logger;
            _connector = new Azure.Iot.Operations.Connector.ManagementActionConnectorWorker(
                applicationContext,
                connectorLogger,
                mqttClient,
                handlerFactory,
                messageSchemaProvider,
                adrClientFactory);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting ManagementActionConnector (simplified)...");
            await _connector.RunConnectorAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _connector.Dispose();
            base.Dispose();
        }
    }

    /// <summary>
    /// Sample factory that creates a <see cref="SampleManagementActionHandler"/> for each
    /// management action discovered on an asset.
    /// </summary>
    public sealed class SampleManagementActionHandlerFactory : IManagementActionHandlerFactory
    {
        private readonly ILogger<SampleManagementActionHandler> _logger;

        public SampleManagementActionHandlerFactory(ILogger<SampleManagementActionHandler> logger)
        {
            _logger = logger;
        }

        public IManagementActionHandler CreateHandler(
            string deviceName,
            Device device,
            string inboundEndpointName,
            string assetName,
            Asset asset,
            string groupName,
            AssetManagementGroupAction action,
            EndpointCredentials? endpointCredentials)
        {
            _logger.LogInformation(
                "Creating handler for {Group}::{Action} (type={ActionType}) on asset {AssetName}",
                groupName, action.Name, action.ActionType, assetName);

            return new SampleManagementActionHandler(_logger, deviceName, assetName, groupName, action.Name);
        }
    }

    /// <summary>
    /// Minimal no-op handler: logs the invocation and returns an empty success response.
    /// Real connectors would parse the payload, execute on the device, and build a typed response.
    /// </summary>
    public sealed class SampleManagementActionHandler : IManagementActionHandler
    {
        private readonly ILogger _logger;
        private readonly string _deviceName;
        private readonly string _assetName;
        private readonly string _groupName;
        private readonly string _actionName;

        public SampleManagementActionHandler(
            ILogger logger,
            string deviceName,
            string assetName,
            string groupName,
            string actionName)
        {
            _logger = logger;
            _deviceName = deviceName;
            _assetName = assetName;
            _groupName = groupName;
            _actionName = actionName;
        }

        public Task<ManagementActionResponse> HandleCallAsync(
            ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "HandleCall: {Group}::{Action} on {Device}/{Asset} ({Bytes} bytes)",
                _groupName, _actionName, _deviceName, _assetName, args.Payload.Length);

            // Real implementation: parse request, invoke device RPC, build response
            return Task.FromResult(new ManagementActionResponse
            {
                Payload = ReadOnlySequence<byte>.Empty,
                ContentType = "application/json",
                CloudEvent = null,
            });
        }

        public Task<ManagementActionResponse> HandleReadAsync(
            ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "HandleRead: {Group}::{Action} on {Device}/{Asset}",
                _groupName, _actionName, _deviceName, _assetName);

            // Real implementation: read value from device, serialize into response
            return Task.FromResult(new ManagementActionResponse
            {
                Payload = ReadOnlySequence<byte>.Empty,
                ContentType = "application/json",
                CloudEvent = null,
            });
        }

        public Task<ManagementActionResponse> HandleWriteAsync(
            ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "HandleWrite: {Group}::{Action} on {Device}/{Asset} ({Bytes} bytes)",
                _groupName, _actionName, _deviceName, _assetName, args.Payload.Length);

            // Real implementation: parse payload, write to device, acknowledge
            return Task.FromResult(new ManagementActionResponse
            {
                Payload = ReadOnlySequence<byte>.Empty,
                ContentType = "application/json",
                CloudEvent = null,
            });
        }

        public ValueTask DisposeAsync()
        {
            _logger.LogInformation(
                "Disposing handler for {Group}::{Action} on {Device}/{Asset}",
                _groupName, _actionName, _deviceName, _assetName);
            return ValueTask.CompletedTask;
        }
    }
}

