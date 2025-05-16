// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.Observability.AkriObservabilityService;

namespace Azure.Iot.Operations.Services.Observability;

public class MetricsReporterService : IAsyncDisposable
{
    private readonly IAkriObservabilityService _observabilityService;
    private readonly MetricsReporterOptions _options;
    private readonly CounterMetricCache _counterCache;
    private readonly GaugeMetricCache _gaugeCache;
    private readonly HistogramMetricCache _histogramCache;
    private readonly ITimerFactory _timerFactory;
    private ITimer? _reportingTimer;
    private bool _isDisposed;
    private readonly SemaphoreSlim _reportingSemaphore = new(1, 1);

    public MetricsReporterService(
        IAkriObservabilityService observabilityService,
        MetricsReporterOptions? options = null,
        ITimerFactory? timerFactory = null)
    {
        _observabilityService = observabilityService ?? throw new ArgumentNullException(nameof(observabilityService));
        _options = options ?? new MetricsReporterOptions();
        _timerFactory = timerFactory ?? new DefaultTimerFactory();

        _counterCache = new CounterMetricCache();
        _gaugeCache = new GaugeMetricCache();
        _histogramCache = new HistogramMetricCache();
    }

    public void Start(CancellationToken cancellationToken = default)
    {
        _reportingTimer = _timerFactory.CreateTimer();

        _reportingTimer.Start(async _ => await ReportMetricsAsync(cancellationToken),
            cancellationToken,
            _options.ReportingInterval,
            _options.ReportingInterval);
    }

    public async Task StopAsync()
    {
        if (_reportingTimer == null)
        {
            return;
        }

        await _reportingTimer.DisposeAsync();
        _reportingTimer = null;
    }

    public ICounter CreateCounter(string name, Dictionary<string, string> labels, string? unit = null)
    {
        ValidateMetricParameters(name, labels);
        return _counterCache.CreateCounter(name, labels, unit);
    }

    public IGauge CreateGauge(string name, Dictionary<string, string> labels, string? unit = null)
    {
        ValidateMetricParameters(name, labels);
        return _gaugeCache.CreateGauge(name, labels, unit);
    }

    public IHistogram CreateHistogram(string name, Dictionary<string, string> labels, string? unit = null)
    {
        ValidateMetricParameters(name, labels);
        return _histogramCache.CreateHistogram(name, labels, unit);
    }

    private void ValidateMetricParameters(string name, Dictionary<string, string> labels)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));
        }

        if (labels == null)
        {
            throw new ArgumentNullException(nameof(labels));
        }
    }

    private async Task ReportMetricsAsync(CancellationToken cancellationToken)
    {
        if (!await _reportingSemaphore.WaitAsync(0, cancellationToken))
        {
            // Another reporting operation is in progress
            return;
        }

        try
        {
            var request = new PublishMetricsRequestPayload
            {
                Metrics = new PublishMetricsRequestSchema
                {
                    CounterMetrics = _counterCache.GetMetricsAndClear(_options.MaxBatchSize),
                    GaugeMetrics = _gaugeCache.GetMetricsAndClear(_options.MaxBatchSize),
                    HistogramMetrics = _histogramCache.GetMetricsAndClear(_options.MaxBatchSize)
                }
            };

            // Only send if there are metrics to report
            if (request.Metrics.CounterMetrics.Count > 0 ||
                request.Metrics.GaugeMetrics.Count > 0 ||
                request.Metrics.HistogramMetrics.Count > 0)
            {
                try
                {
                    RpcCallAsync<PublishMetricsResponsePayload> response = _observabilityService.PublishMetricsAsync(request);
                }
                catch (Exception ex)
                {
                    // Log but don't throw
                    System.Diagnostics.Debug.WriteLine($"Error reporting metrics: {ex}");
                }
            }
        }
        finally
        {
            _reportingSemaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_reportingTimer != null)
        {
            await _reportingTimer.DisposeAsync();
            _reportingTimer = null;
        }

        _reportingSemaphore.Dispose();
        _isDisposed = true;
    }
}
