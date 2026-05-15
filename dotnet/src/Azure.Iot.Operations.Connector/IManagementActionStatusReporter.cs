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

        /// <summary>
        /// Reports the action as unavailable with a free-form diagnostic message.
        /// </summary>
        /// <remarks>
        /// Use this only when the connector has actually attempted to interact with the device
        /// and observed a runtime failure (e.g. unreachable endpoint, handler exception). For
        /// configuration-time problems where no probe was performed, call
        /// <see cref="PauseHealthReportingAsync"/> instead so the runtime-health status lapses
        /// to <c>Unknown</c> rather than falsely asserting that the device is broken.
        /// </remarks>
        Task ReportUnavailableAsync(string? message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops publishing runtime-health updates for this action so that the ADR-side status
        /// is allowed to lapse to <c>Unknown</c>. Use when a <see cref="ConfigError"/> means the
        /// connector cannot determine the action's true runtime health — reporting
        /// <c>Unavailable</c> in that case would make an unjustified claim about the device.
        /// </summary>
        Task PauseHealthReportingAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the action's <c>Config</c> status. Pass <c>null</c> to clear an existing
        /// configuration error.
        /// </summary>
        Task ReportConfigErrorAsync(ConfigError? validationError, CancellationToken cancellationToken = default);
    }
}

