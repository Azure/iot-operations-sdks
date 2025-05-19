// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.Observability;
using Azure.Iot.Operations.Services.Observability.AkriObservabilityService;
using Azure.Iot.Operations.Services.Observability.Utils;
using Moq;
using Xunit;
using ITimer = Azure.Iot.Operations.Services.Observability.Utils.ITimer;

namespace Azure.Iot.Operations.Services.UnitTests.Observability;

public class MetricsReporterServiceTests
{
    private readonly Mock<IAkriObservabilityService> _mockObservabilityService;
    private readonly Mock<ITimerFactory> _mockTimerFactory;
    private readonly Mock<ITimer> _mockTimer;
    private readonly MetricsReporterOptions _options;
    private readonly Dictionary<string, string> _defaultLabels;

    public MetricsReporterServiceTests()
    {
        _mockObservabilityService = new Mock<IAkriObservabilityService>();
        _mockTimerFactory = new Mock<ITimerFactory>();
        _mockTimer = new Mock<ITimer>();
        _options = new MetricsReporterOptions
        {
            ReportingInterval = TimeSpan.FromSeconds(1),
            MaxBatchSize = 100
        };
        _defaultLabels = new Dictionary<string, string> { { "service", "test" } };

        // Setup mock timer
        _mockTimerFactory.Setup(f => f.CreateTimer()).Returns(_mockTimer.Object);
        _mockTimer.Setup(t => t.Start(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
            .Callback<Func<CancellationToken, Task>, CancellationToken, TimeSpan, TimeSpan>((callback, token, dueTime, period) =>
            {
                // Store the callback for later invocation
                _timerCallback = callback;
            });
    }

    private Func<CancellationToken, Task>? _timerCallback;

    [Fact]
    public void CreateCounter_ValidParameters_ReturnsCounter()
    {
        // Arrange
        var service = new MetricsReporterService(_mockObservabilityService.Object, _options, _mockTimerFactory.Object);

        // Act
        var counter = service.CreateCounter("test_counter", _defaultLabels, "bytes");

        // Assert
        Assert.NotNull(counter);
    }

    [Fact]
    public void CreateGauge_ValidParameters_ReturnsGauge()
    {
        // Arrange
        var service = new MetricsReporterService(_mockObservabilityService.Object, _options, _mockTimerFactory.Object);

        // Act
        var gauge = service.CreateGauge("test_gauge", _defaultLabels, "seconds");

        // Assert
        Assert.NotNull(gauge);
    }

    [Fact]
    public void CreateHistogram_ValidParameters_ReturnsHistogram()
    {
        // Arrange
        var service = new MetricsReporterService(_mockObservabilityService.Object, _options, _mockTimerFactory.Object);

        // Act
        var histogram = service.CreateHistogram("test_histogram", _defaultLabels, "bytes");

        // Assert
        Assert.NotNull(histogram);
    }

    [Fact]
    public void Start_CreatesAndStartsTimer()
    {
        // Arrange
        var service = new MetricsReporterService(_mockObservabilityService.Object, _options, _mockTimerFactory.Object);

        // Act
        service.Start();

        // Assert
        _mockTimerFactory.Verify(f => f.CreateTimer(), Times.Once);
        _mockTimer.Verify(t => t.Start(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(),
            _options.ReportingInterval, _options.ReportingInterval), Times.Once);
    }

    [Fact]
    public async Task Stop_DisposesTimer()
    {
        // Arrange
        var service = new MetricsReporterService(_mockObservabilityService.Object, _options, _mockTimerFactory.Object);
        service.Start();

        // Act
        await service.StopAsync();

        // Assert
        _mockTimer.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task ReportMetrics_SendsMetricsToObservabilityService()
    {
        // Arrange
        _mockObservabilityService
            .Setup(s => s.PublishMetricsAsync(It.IsAny<PublishMetricsRequestPayload>()))
            .Returns(new RpcCallAsync<PublishMetricsResponsePayload>());

        var service = new MetricsReporterService(_mockObservabilityService.Object, _options, _mockTimerFactory.Object);

        // Create and update some metrics
        var counter = service.CreateCounter("test_counter", _defaultLabels);
        counter.Add(5);

        var gauge = service.CreateGauge("test_gauge", _defaultLabels);
        gauge.Record(10);

        var histogram = service.CreateHistogram("test_histogram", _defaultLabels);
        histogram.Record(15);

        service.Start();

        // Act - Trigger the timer callback manually
        if (_timerCallback != null)
        {
            await _timerCallback(CancellationToken.None);
        }

        // Assert
        _mockObservabilityService.Verify(s => s.PublishMetricsAsync(
                It.Is<PublishMetricsRequestPayload>(p =>
                    p.Metrics.CounterMetrics!.Count > 0 &&
                    p.Metrics.GaugeMetrics!.Count > 0 &&
                    p.Metrics.HistogramMetrics!.Count > 0)),
            Times.Once);

        // verify all fields of the PublishMetricsRequestPayload and nested classes
        _mockObservabilityService.Verify(s => s.PublishMetricsAsync(
                It.Is<PublishMetricsRequestPayload>(p =>
                    p.Metrics.CounterMetrics![0].Definition.Name == "test_counter" &&
                    p.Metrics.CounterMetrics![0].Definition.Labels["service"] == "test" &&
                    Math.Abs(p.Metrics.CounterMetrics![0].Operations[0].Value - 5) < 0.001 &&
                    p.Metrics.GaugeMetrics![0].Definition.Name == "test_gauge" &&
                    p.Metrics.GaugeMetrics![0].Definition.Labels["service"] == "test" &&
                    Math.Abs(p.Metrics.GaugeMetrics![0].Operations[0].Value - 10) < 0.001 &&
                    p.Metrics.HistogramMetrics![0].Definition.Name == "test_histogram" &&
                    p.Metrics.HistogramMetrics![0].Definition.Labels["service"] == "test" &&
                    Math.Abs(p.Metrics.HistogramMetrics![0].Operations[0].Value - 15) < 0.001)),
            Times.Once);
    }

    [Fact]
    public async Task Dispose_CleanupResources()
    {
        // Arrange
        var service = new MetricsReporterService(_mockObservabilityService.Object, _options, _mockTimerFactory.Object);
        service.Start();

        // Act
        await service.DisposeAsync();

        // Assert
        _mockTimer.Verify(t => t.DisposeAsync(), Times.Once);
    }
}
