// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using ManagementActionConnector.Devices;

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
                _ => throw new InvalidOperationException(
                    $"CreateHandler called for unsupported action '{action.Name}'. "
                    + $"The worker should have filtered this via {nameof(SupportsAction)}."),
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

        public bool SupportsAction(AssetManagementGroupAction action)
        {
            return action.Name switch
            {
                "reboot" => true,
                "read-temperature" => true,
                "write-configuration" => true,
                _ => false
            };
        }
    }
}
