// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Narrow, per-action view over the status-reporting surface of <see cref="AssetClient"/>.
    /// Bound to a single (managementGroup, action) pair so callers cannot accidentally
    /// report against the wrong action.
    /// </summary>
    /// <remarks>
    /// Handlers receive an instance via <see cref="IManagementActionHandlerFactory.CreateHandler"/>
    /// and may use it to publish health transitions discovered at runtime — for example, when
    /// a target URI becomes unreachable mid-shift. Configuration-time validation should instead
    /// be reported by returning a <see cref="ConfigError"/> from
    /// <see cref="IManagementActionHandlerFactory.ValidateConfigurationAsync"/>.
    /// </remarks>
    public interface IManagementActionStatusReporter
    {
        /// <summary>Reports the action as available (clears any prior runtime-health error).</summary>
        Task ReportAvailableAsync(CancellationToken cancellationToken = default);

        /// <summary>Reports the action as unavailable with a free-form diagnostic message.</summary>
        Task ReportUnavailableAsync(string? message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the action's <c>Config</c> status. Pass <c>null</c> to clear an existing
        /// configuration error.
        /// </summary>
        Task ReportConfigErrorAsync(ConfigError? validationError, CancellationToken cancellationToken = default);
    }
}

