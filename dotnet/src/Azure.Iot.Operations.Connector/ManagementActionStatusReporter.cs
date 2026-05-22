// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Default <see cref="IManagementActionStatusReporter"/> implementation that delegates
    /// to an <see cref="AssetClient"/>. Constructed by <see cref="ConnectorWorker"/>
    /// once per action and shared between the worker's notification loop and the user's
    /// handler (via the factory).
    /// </summary>
    internal sealed class ManagementActionStatusReporter : IManagementActionStatusReporter
    {
        private readonly AssetClient _assetClient;
        private readonly string _groupName;
        private readonly string _actionName;

        public ManagementActionStatusReporter(AssetClient assetClient, string groupName, string actionName)
        {
            _assetClient = assetClient;
            _groupName = groupName;
            _actionName = actionName;
        }

        public Task ReportAvailableAsync(CancellationToken cancellationToken = default)
            => _assetClient.ReportManagementActionRuntimeHealthAsync(
                _groupName, _actionName,
                new ConnectorRuntimeHealth { Status = HealthStatus.Available },
                cancellationToken: cancellationToken);

        public Task ReportUnavailableAsync(string? message, CancellationToken cancellationToken = default)
            => _assetClient.ReportManagementActionRuntimeHealthAsync(
                _groupName, _actionName,
                new ConnectorRuntimeHealth
                {
                    Status = HealthStatus.Unavailable,
                    ReasonCode = "ConfigError",
                    Message = message,
                },
                cancellationToken: cancellationToken);

        public Task PauseHealthReportingAsync(CancellationToken cancellationToken = default)
            => _assetClient.PauseManagementActionRuntimeHealthReportingAsync(
                _groupName, _actionName, cancellationToken);

        public Task ReportConfigErrorAsync(ConfigError? validationError, CancellationToken cancellationToken = default)
            => _assetClient.GetAndUpdateAssetStatusAsync(
                current =>
                {
                    current.Config ??= new ConfigStatus();
                    current.Config.LastTransitionTime = DateTime.UtcNow;
                    current.UpdateManagementGroupStatus(
                        _groupName,
                        new AssetManagementGroupActionStatus
                        {
                            Name = _actionName,
                            Error = validationError,
                        });
                    return current;
                },
                onlyIfChanged: true,
                commandTimeout: null,
                cancellationToken);
    }
}

