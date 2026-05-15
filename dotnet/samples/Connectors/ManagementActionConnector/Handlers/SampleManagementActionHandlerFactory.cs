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
            EndpointCredentials? endpointCredentials,
            IManagementActionStatusReporter statusReporter)
        {
            ILogger logger = _loggerFactory.CreateLogger(action.Name);
            logger.LogInformation(
                "Creating handler for action {Action} (type={ActionType}, targetUri={Target})",
                action.Name, action.ActionType, action.TargetUri);

            return action.Name switch
            {
                "reboot" => new RebootHandler(logger, _device, statusReporter),
                "read-temperature" => new ReadTemperatureHandler(logger, _device),
                "write-configuration" => new WriteConfigurationHandler(logger, _device),
                _ => new UnknownActionHandler(logger, action.Name),
            };
        }

        /// <summary>
        /// Connector-specific validation. Called at startup and on every definition change.
        /// Return <c>null</c> if the definition is valid; return a <see cref="ConfigError"/>
        /// to surface a config-time issue back to ADR (the action will be reported Unavailable).
        /// </summary>
        /// <remarks>
        /// This sample only checks the target URI scheme — enough to show where validation goes.
        /// Real connectors might also validate timeout ranges, parse <c>ActionConfiguration</c>
        /// as JSON, or confirm the device endpoint is reachable.
        /// </remarks>
        public ValueTask<ConfigError?> ValidateConfigurationAsync(
            Device device,
            string inboundEndpointName,
            Asset asset,
            AssetManagementGroupAction action,
            CancellationToken cancellationToken)
        {
            if (!action.TargetUri.StartsWith("device://", StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult<ConfigError?>(new ConfigError
                {
                    Code = "UnsupportedTargetUriScheme",
                    Message = $"This connector only handles 'device://' targets; got '{action.TargetUri}'.",
                });
            }

            return ValueTask.FromResult<ConfigError?>(null);
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
