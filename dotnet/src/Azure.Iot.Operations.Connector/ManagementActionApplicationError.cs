// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Application-level error returned by the connector for a management action invocation.
    /// Set on <see cref="ManagementActionResponse.ApplicationError"/> when the action was
    /// executed but the outcome is a domain-level failure (vs. a transport error).
    /// </summary>
    public record ManagementActionApplicationError
    {
        /// <summary>Caller-facing error code (connector-defined).</summary>
        public required string ErrorCode { get; set; }

        /// <summary>Human-readable payload describing the error. May be empty.</summary>
        public string ErrorPayload { get; set; } = string.Empty;
    }
}

