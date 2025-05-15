// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Observability;

public class MetricsReporterOptions
{
    /// <summary>
    /// Interval at which metrics are reported to the underlying service
    /// </summary>
    public TimeSpan ReportingInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Maximum number of operations to include in a single report
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;
}
