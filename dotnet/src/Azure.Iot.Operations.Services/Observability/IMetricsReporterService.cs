// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Observability;

public interface IMetricsReporterService
{
    void Start(CancellationToken cancellationToken = default);
    Task StopAsync();
    ICounter CreateCounter(string name, Dictionary<string, string> labels, string? unit = null);
    IGauge CreateGauge(string name, Dictionary<string, string> labels, string? unit = null);
    IHistogram CreateHistogram(string name, Dictionary<string, string> labels, string? unit = null);
}
