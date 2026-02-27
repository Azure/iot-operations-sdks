// <copyright file="SMBConnectorOptions.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Akri.Connector.SMB;

/// <summary>
/// Configuration options for the SMB connector.
/// </summary>
public sealed class SMBConnectorOptions
{
    /// <summary>
    /// Gets or sets the connection timeout in seconds.
    /// </summary>
    [Range(1, 300)]
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of concurrent SMB connections.
    /// </summary>
    [Range(1, 100)]
    public int MaxConcurrentConnections { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum file size in bytes (files larger than this are skipped).
    /// </summary>
    [Range(1, 104857600)] // Max 100MB
    public long MaxFileSizeBytes { get; set; } = 10485760; // 10MB

    /// <summary>
    /// Gets or sets whether to enable Kubernetes leader election for multi-pod deployments.
    /// </summary>
    public bool EnableLeaderElection { get; set; } = false;

    /// <summary>
    /// Gets or sets the Kubernetes namespace for leader election.
    /// </summary>
    public string LeaderElectionNamespace { get; set; } = "default";

    /// <summary>
    /// Gets or sets the lease name for leader election.
    /// </summary>
    public string LeaderElectionLeaseName { get; set; } = "smb-connector-leader";

    /// <summary>
    /// Gets or sets the connector instance ID for state store key namespacing.
    /// </summary>
    public string InstanceId { get; set; } = "smb-connector";

    /// <summary>
    /// Gets or sets the task type for query execution.
    /// Valid values: "Parse" (CSV parsing), "Copy" (file sync to local storage).
    /// </summary>
    public string TaskType { get; set; } = "Parse";

    /// <summary>
    /// Gets or sets the destination path on Kubernetes PV for Copy task type.
    /// Required when TaskType is "Copy".
    /// </summary>
    public string DestinationPath { get; set; } = string.Empty;
}
