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
            Device device,
            string inboundEndpointName,
            Asset asset,
            AssetManagementGroupAction action,
            EndpointCredentials? endpointCredentials)
        {
            ILogger logger = _loggerFactory.CreateLogger(action.Name);
            logger.LogInformation(
                "Creating handler for action {Action} (type={ActionType}, targetUri={Target})",
                action.Name, action.ActionType, action.TargetUri);

            return action.Name switch
            {
                "reboot" => new RebootHandler(logger, _device),
                "read-temperature" => new ReadTemperatureHandler(logger, _device),
                "write-configuration" => new WriteConfigurationHandler(logger, _device),
                _ => new UnknownActionHandler(logger, action.Name),
            };
        }

        /// <summary>Fallback handler for an action name the sample doesn't recognize.</summary>
        private sealed class UnknownActionHandler : IManagementActionHandler
        {
            private readonly ILogger _logger;
            private readonly string _actionName;

            public UnknownActionHandler(ILogger logger, string actionName)
            {
                _logger = logger;
                _actionName = actionName;
            }

            public Task<ManagementActionResponse> HandleCallAsync(ManagementActionInvokedEventArgs args, CancellationToken ct) => Reject(args);
            public Task<ManagementActionResponse> HandleReadAsync(ManagementActionInvokedEventArgs args, CancellationToken ct) => Reject(args);
            public Task<ManagementActionResponse> HandleWriteAsync(ManagementActionInvokedEventArgs args, CancellationToken ct) => Reject(args);

            private Task<ManagementActionResponse> Reject(ManagementActionInvokedEventArgs args)
            {
                _logger.LogWarning("Received invocation for unknown action {Group}::{Action}", args.GroupName, args.ActionName);
                return Task.FromResult(ResponseHelpers.ApplicationError(
                    "UnknownAction",
                    $"Sample connector has no handler registered for {args.GroupName}::{_actionName}."));
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
