// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using ManagementActionConnector.Devices;
using Microsoft.Extensions.Logging;

namespace ManagementActionConnector.Handlers
{
    /// <summary>
    /// Creates the right <see cref="IManagementActionHandler"/> for each action declared
    /// on the asset. Dispatch is by action name (one of each type in the sample asset YAML).
    /// </summary>
    public sealed class SampleManagementActionHandlerFactory : IManagementActionHandlerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly FakeDevice _device;

        public SampleManagementActionHandlerFactory(ILoggerFactory loggerFactory, FakeDevice device)
        {
            _loggerFactory = loggerFactory;
            _device = device;
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
            ILogger logger = _loggerFactory.CreateLogger($"{groupName}.{action.Name}");
            logger.LogInformation(
                "Creating handler for {Group}::{Action} (type={ActionType}, targetUri={Target}) on asset {Asset}",
                groupName, action.Name, action.ActionType, action.TargetUri, assetName);

            return action.Name switch
            {
                "reboot" => new RebootHandler(logger, _device, deviceName, assetName),
                "read-temperature" => new ReadTemperatureHandler(logger, _device, deviceName, assetName),
                "write-configuration" => new WriteConfigurationHandler(logger, _device, deviceName, assetName),
                _ => new UnknownActionHandler(logger, groupName, action.Name),
            };
        }

        /// <summary>Fallback handler for an action name the sample doesn't recognize.</summary>
        private sealed class UnknownActionHandler : IManagementActionHandler
        {
            private readonly ILogger _logger;
            private readonly string _groupName;
            private readonly string _actionName;

            public UnknownActionHandler(ILogger logger, string groupName, string actionName)
            {
                _logger = logger;
                _groupName = groupName;
                _actionName = actionName;
            }

            public Task<ManagementActionResponse> HandleCallAsync(ManagementActionInvokedEventArgs args, CancellationToken ct) => Reject();
            public Task<ManagementActionResponse> HandleReadAsync(ManagementActionInvokedEventArgs args, CancellationToken ct) => Reject();
            public Task<ManagementActionResponse> HandleWriteAsync(ManagementActionInvokedEventArgs args, CancellationToken ct) => Reject();

            private Task<ManagementActionResponse> Reject()
            {
                _logger.LogWarning("Received invocation for unknown action {Group}::{Action}", _groupName, _actionName);
                return Task.FromResult(ResponseHelpers.ApplicationError(
                    "UnknownAction",
                    $"Sample connector has no handler registered for {_groupName}::{_actionName}."));
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
