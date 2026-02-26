// <copyright file="HistorianConnectorWorker.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Collections.Concurrent;
using Akri.HistorianConnector.Core.Adapters;
using Akri.HistorianConnector.Core.Contracts;
using Akri.HistorianConnector.Core.Models;
using Akri.HistorianConnector.Core.StateStore;
using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Microsoft.Extensions.Logging;
using ResilientQueryRunner;

namespace Akri.HistorianConnector.Core;

/// <summary>
/// A connector worker for historian systems that uses the ResilientQueryRunner
/// for time-windowed batch queries with watermark tracking.
/// </summary>
public class HistorianConnectorWorker : ConnectorWorker
{
    private const string EndpointTopicMismatchMessage = "endpoint information does not match the topic";

    private readonly ApplicationContext _applicationContext;
    private readonly IHistorianQueryExecutor _queryExecutor;
    private readonly IAssetToQueryMapper _queryMapper;
    private readonly IHistorianBatchSerializer _batchSerializer;
    private readonly HistorianConnectorOptions _options;
    private readonly ILogger<HistorianConnectorWorker> _historianLogger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly ConcurrentDictionary<string, HistorianQueryDefinition> _activeQueries = new();
    private readonly ConcurrentDictionary<string, QueryRuntimeContext> _queryContexts = new();

    private StateStoreCheckpointRepository? _checkpointRepository;
    private RqrEngine? _rqrEngine;
    private Task? _rqrRunnerTask;
    private CancellationTokenSource? _rqrCts;
    private readonly SemaphoreSlim _rqrInitLock = new(1, 1);
    private bool _rqrInitialized;
    private bool _skipAdrStatusUpdates;

    private readonly IMqttClient _mqttClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistorianConnectorWorker"/> class.
    /// </summary>
    public HistorianConnectorWorker(
        ApplicationContext applicationContext,
        ILogger<ConnectorWorker> connectorLogger,
        ILogger<HistorianConnectorWorker> historianLogger,
        ILoggerFactory loggerFactory,
        IMqttClient mqttClient,
        IMessageSchemaProvider messageSchemaProvider,
        IAzureDeviceRegistryClientWrapperProvider adrClientFactory,
        IHistorianQueryExecutor queryExecutor,
        IAssetToQueryMapper queryMapper,
        IHistorianBatchSerializer batchSerializer,
        HistorianConnectorOptions options,
        IConnectorLeaderElectionConfigurationProvider? leaderElectionProvider = null)
        : base(applicationContext, connectorLogger, mqttClient, messageSchemaProvider, adrClientFactory, leaderElectionProvider)
    {
        _applicationContext = applicationContext;
        _queryExecutor = queryExecutor;
        _queryMapper = queryMapper;
        _batchSerializer = batchSerializer;
        _options = options;
        _historianLogger = historianLogger;
        _loggerFactory = loggerFactory;
        _mqttClient = mqttClient;

        // Wire up the SDK's asset lifecycle callbacks
        WhileDeviceIsAvailable = OnDeviceAvailableAsync;
        WhileAssetIsAvailable = OnAssetAvailableAsync;
    }

    /// <summary>
    /// Called when the connector starts.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _historianLogger.LogInformation("Historian connector starting. Instance: {InstanceId}", _options.ConnectorInstanceId);

        // Create the CTS now so EnsureRqrEngineInitializedAsync can use it
        // when the first device callback fires (after MQTT has connected).
        _rqrCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Let the base class connect to MQTT and start ADR observation.
        // State-store and RQR engine initialisation is deferred to
        // EnsureRqrEngineInitializedAsync, which runs inside the first
        // OnDeviceAvailableAsync callback where MQTT is guaranteed connected.
        await base.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Initializes the StateStore checkpoint repository and the RQR engine the first time
    /// it is called. All subsequent calls return immediately. Must be called from a device
    /// or asset callback, where the MQTT connection is guaranteed to be established.
    /// </summary>
    private async Task EnsureRqrEngineInitializedAsync(CancellationToken cancellationToken)
    {
        if (_rqrInitialized)
        {
            return; // fast path — no lock needed after first init
        }

        await _rqrInitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_rqrInitialized)
            {
                return; // double-checked inside the lock
            }

            // MQTT is connected at this point — the SDK only fires device/asset callbacks
            // after a successful connection, so StateStoreCheckpointRepository is safe to create.
            try
            {
                _checkpointRepository = new StateStoreCheckpointRepository(
                    _applicationContext,
                    _mqttClient,
                    _options.ConnectorInstanceId,
                    _loggerFactory.CreateLogger<StateStoreCheckpointRepository>(),
                    _loggerFactory);
            }
            catch (Exception ex)
            {
                _historianLogger.LogWarning(ex, "Failed to initialize StateStoreCheckpointRepository; continuing without persisted checkpoints");
                _checkpointRepository = null;
            }

            InitializeRqrEngine();
            _rqrRunnerTask = RunRqrEngineAsync(_rqrCts!.Token);

            _rqrInitialized = true;
        }
        finally
        {
            _rqrInitLock.Release();
        }
    }

    private void InitializeRqrEngine()
    {
        var stateStore = new RqrStateStoreAdapter(_checkpointRepository!, _historianLogger);
        var executor = new RqrExecutorAdapter(_queryExecutor, _activeQueries, _historianLogger);
        var resultHandler = new RqrResultHandlerAdapter(this, _batchSerializer, _activeQueries, _historianLogger);
        var mappingProvider = new RqrMappingProviderAdapter(_activeQueries);

        _rqrEngine = new RqrEngine(
            _options.ConnectorInstanceId,
            stateStore,
            executor,
            resultHandler,
            mappingProvider,
            _options.GlobalConcurrencyLimit);

        _historianLogger.LogInformation("RQR engine initialized with concurrency limit {Limit}", _options.GlobalConcurrencyLimit);
        // Register any queries that may have been added prior to engine
        // initialization (for example, via configuration or ad-hoc calls).
        foreach (var kv in _activeQueries)
        {
            try
            {
                var rqrQuery = MapToRqrQuery(kv.Value);
                _rqrEngine.RegisterQuery(rqrQuery);
                _historianLogger.LogInformation("Registered preconfigured query {QueryId}", kv.Key);
            }
            catch (Exception ex)
            {
                _historianLogger.LogError(ex, "Failed to register preconfigured query {QueryId}", kv.Key);
            }
        }
    }

    /// <summary>
    /// Registers a historian query definition with the worker and RQR engine.
    /// This can be called from configuration loaders or other runtime code to
    /// add queries dynamically (ad-hoc) or from startup configuration.
    /// </summary>
    /// <param name="query">The query definition to register.</param>
    public void RegisterQuery(HistorianQueryDefinition query)
    {
        ArgumentNullException.ThrowIfNull(query);

        _activeQueries[query.QueryId] = query;

        if (_rqrEngine != null)
        {
            try
            {
                var rqrQuery = MapToRqrQuery(query);
                _rqrEngine.RegisterQuery(rqrQuery);
                _historianLogger.LogInformation("Registered query {QueryId}", query.QueryId);
            }
            catch (Exception ex)
            {
                _historianLogger.LogError(ex, "Failed to register query {QueryId}", query.QueryId);
            }
        }
        else
        {
            _historianLogger.LogInformation("Stored query {QueryId} and will register when RQR engine initializes", query.QueryId);
        }
    }

    /// <summary>
    /// Registers multiple historian queries.
    /// </summary>
    public void RegisterQueries(IEnumerable<HistorianQueryDefinition> queries)
    {
        if (queries == null)
        {
            return;
        }

        foreach (var q in queries)
        {
            RegisterQuery(q);
        }
    }

    private async Task RunRqrEngineAsync(CancellationToken cancellationToken)
    {
        var tickInterval = TimeSpan.FromSeconds(_options.TickIntervalSeconds);

        _historianLogger.LogInformation("Starting RQR engine tick loop. Interval: {Interval}", tickInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _rqrEngine!.TickAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _historianLogger.LogError(ex, "Error during RQR engine tick");
            }

            try
            {
                await Task.Delay(tickInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _historianLogger.LogInformation("RQR engine tick loop stopped");
    }

    /// <summary>
    /// Called by the SDK when a device becomes available.
    /// </summary>
    private async Task OnDeviceAvailableAsync(DeviceAvailableEventArgs args, CancellationToken cancellationToken)
    {
        _historianLogger.LogInformation(
            "Device available: {DeviceName}, Endpoint: {EndpointName}",
            args.DeviceName,
            args.InboundEndpointName);

        await EnsureRqrEngineInitializedAsync(cancellationToken).ConfigureAwait(false);

        await TryReportStatusAsync(
            statusToken => args.DeviceEndpointClient.GetAndUpdateDeviceStatusAsync(
                currentDeviceStatus =>
                {
                    currentDeviceStatus.Config ??= new();
                    currentDeviceStatus.Config.LastTransitionTime = DateTime.UtcNow;
                    currentDeviceStatus.Config.Error = null;
                    return currentDeviceStatus;
                },
                true,
                TimeSpan.FromSeconds(5),
                statusToken),
            $"device status as healthy for {args.DeviceName}",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Called by the SDK when an asset becomes available.
    /// </summary>
    private async Task OnAssetAvailableAsync(AssetAvailableEventArgs args, CancellationToken cancellationToken)
    {
        _historianLogger.LogInformation(
            "Asset available: {AssetName} on device {DeviceName}",
            args.AssetName,
            args.DeviceName);

        await EnsureRqrEngineInitializedAsync(cancellationToken).ConfigureAwait(false);

        if (args.Asset.Datasets == null || args.Asset.Datasets.Count == 0)
        {
            _historianLogger.LogDebug("Asset {AssetName} has no datasets, skipping", args.AssetName);
            return;
        }

        var registeredQueryIds = new List<string>();

        foreach (var dataset in args.Asset.Datasets)
        {
            var queryIds = await RegisterDatasetQueryAsync(
                args,
                dataset,
                cancellationToken).ConfigureAwait(false);

            registeredQueryIds.AddRange(queryIds);
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Asset became unavailable.
        }

        foreach (var queryId in registeredQueryIds)
        {
            _activeQueries.TryRemove(queryId, out _);
            _queryContexts.TryRemove(queryId, out _);

            if (_rqrEngine != null)
            {
                var removed = _rqrEngine.UnregisterQuery(new QueryId(queryId));
                if (removed)
                {
                    _historianLogger.LogInformation("Unregistered query {QueryId} for unavailable asset {AssetName}", queryId, args.AssetName);
                }
            }
        }
    }

    private async Task<IReadOnlyList<string>> RegisterDatasetQueryAsync(
        AssetAvailableEventArgs args,
        AssetDataset dataset,
        CancellationToken cancellationToken)
    {
        var queries = _queryMapper.MapDatasetToQueries(
            args.DeviceName,
            args.Device,
            args.InboundEndpointName,
            args.AssetName,
            args.Asset,
            dataset);

        if (queries.Count == 0)
        {
            _historianLogger.LogDebug(
                "Dataset {DatasetName} on asset {AssetName} was not mapped to a query",
                dataset.Name, args.AssetName);
            return Array.Empty<string>();
        }

        var registeredQueryIds = new List<string>();

        foreach (var query in queries)
        {
            // Validate the query with the historian
            var validation = await _queryExecutor.ValidateAsync(query, cancellationToken).ConfigureAwait(false);

            if (!validation.IsValid)
            {
                _historianLogger.LogError(
                    "Query validation failed for {QueryId}: {Errors}",
                    query.QueryId,
                    string.Join(", ", validation.Errors));
                continue;
            }

            foreach (var warning in validation.Warnings)
            {
                _historianLogger.LogWarning("Query validation warning for {QueryId}: {Warning}", query.QueryId, warning);
            }

            // Store the query definition
            _activeQueries[query.QueryId] = query;
            _queryContexts[query.QueryId] = CreateQueryContext(args, dataset);

            // Convert to RQR QueryDefinition and register
            var rqrQuery = MapToRqrQuery(query);
            _rqrEngine!.RegisterQuery(rqrQuery);

            _historianLogger.LogInformation(
                "Registered historian query {QueryId}: cron='{Cron}', window={Window}s, " +
                "delay={Delay}s, overlap={Overlap}s, basePath='{BasePath}', pattern='{Pattern}', " +
                "host='{Host}', share='{Share}', dataPoints={DataPoints}",
                query.QueryId,
                query.CronExpression,
                query.WindowDuration?.TotalSeconds,
                query.AvailabilityDelay.TotalSeconds,
                query.Overlap.TotalSeconds,
                query.BasePath,
                query.FilePattern,
                query.Host,
                query.ShareName,
                query.DataPoints.Count);

            registeredQueryIds.Add(query.QueryId);
        }

        return registeredQueryIds;
    }

    private QueryRuntimeContext CreateQueryContext(AssetAvailableEventArgs args, AssetDataset dataset)
    {
        return new QueryRuntimeContext
        {
            ForwardAsync = (payload, cancellationToken) => args.AssetClient.ForwardSampledDatasetAsync(dataset, payload),
            ReportAssetHealthyAsync = cancellationToken => args.AssetClient.GetAndUpdateAssetStatusAsync(
                currentAssetStatus =>
                {
                    currentAssetStatus.Config ??= new();
                    currentAssetStatus.Config.Error = null;
                    currentAssetStatus.Config.LastTransitionTime = DateTime.UtcNow;
                    var datasetName = dataset.Name ?? "default";
                    currentAssetStatus.UpdateDatasetStatus(new AssetDatasetEventStreamStatus
                    {
                        Name = datasetName,
                        MessageSchemaReference = args.AssetClient.GetRegisteredDatasetMessageSchema(datasetName),
                        Error = null
                    });
                    return currentAssetStatus;
                },
                true,
                null,
                cancellationToken),
            ReportDeviceHealthyAsync = cancellationToken => args.DeviceEndpointClient.GetAndUpdateDeviceStatusAsync(
                currentDeviceStatus =>
                {
                    currentDeviceStatus.Config ??= new();
                    currentDeviceStatus.Config.LastTransitionTime = DateTime.UtcNow;
                    currentDeviceStatus.Config.Error = null;
                    return currentDeviceStatus;
                },
                true,
                null,
                cancellationToken),
            ReportDeviceErrorAsync = (message, cancellationToken) => args.DeviceEndpointClient.GetAndUpdateDeviceStatusAsync(
                currentDeviceStatus =>
                {
                    currentDeviceStatus.Config ??= new ConfigStatus();
                    currentDeviceStatus.Config.Error = new ConfigError { Message = message };
                    currentDeviceStatus.Config.LastTransitionTime = DateTime.UtcNow;
                    return currentDeviceStatus;
                },
                true,
                null,
                cancellationToken)
        };
    }

    private QueryDefinition MapToRqrQuery(HistorianQueryDefinition query)
    {
        var cronExpression = NormalizeCronExpression(query.CronExpression);
        var cron = CronExpression.Parse(cronExpression);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(query.TimeZoneId);

        return new QueryDefinition
        {
            QueryId = new QueryId(query.QueryId),
            Cron = cron,
            TimeZone = timeZone,
            WatermarkKind = query.WatermarkKind == Models.WatermarkKind.Time
                ? ResilientQueryRunner.WatermarkKind.Time
                : ResilientQueryRunner.WatermarkKind.Index,
            RangeParameters = new RangeParameters
            {
                WindowDuration = query.WindowDuration,
                AvailabilityDelay = query.AvailabilityDelay,
                Overlap = query.Overlap,
                CatchUpMaxWindowsPerTick = query.MaxWindowsPerTick
            },
            Payload = query
        };
    }

    /// <summary>
    /// Forwards a historian batch using the SDK's asset forwarding path.
    /// </summary>
    internal async Task PublishBatchAsync(
        HistorianBatch batch,
        byte[] serializedPayload,
        CancellationToken cancellationToken)
    {
        if (!_queryContexts.TryGetValue(batch.QueryId, out var queryContext))
        {
            _historianLogger.LogWarning("Cannot publish batch for unknown query {QueryId}", batch.QueryId);
            return;
        }

        var payload = serializedPayload.Length > 0
            ? serializedPayload
            : _batchSerializer.Serialize(batch);

        try
        {
            await queryContext.ForwardAsync(payload, cancellationToken).ConfigureAwait(false);
            _historianLogger.LogDebug("Forwarded sampled dataset for query {QueryId}", batch.QueryId);

            await TryReportStatusAsync(
                queryContext.ReportAssetHealthyAsync,
                $"asset status as healthy for query {batch.QueryId}",
                cancellationToken).ConfigureAwait(false);

            await TryReportStatusAsync(
                queryContext.ReportDeviceHealthyAsync,
                $"device status as healthy for query {batch.QueryId}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _historianLogger.LogError(ex, "Failed to forward sampled dataset for query {QueryId}", batch.QueryId);

            await TryReportStatusAsync(
                statusToken => queryContext.ReportDeviceErrorAsync(
                    $"Unable to forward sampled dataset. Error message: {ex.Message}",
                    statusToken),
                $"device error status for query {batch.QueryId}",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TryReportStatusAsync(
        Func<CancellationToken, Task> reporter,
        string context,
        CancellationToken cancellationToken)
    {
        if (_skipAdrStatusUpdates)
        {
            return;
        }

        using var statusCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        statusCts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await reporter(statusCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsEndpointTopicMismatch(ex))
        {
            _skipAdrStatusUpdates = true;
            _historianLogger.LogWarning(
                ex,
                "ADR status updates are being disabled after endpoint/topic mismatch while reporting {Context}. Verify the AIO inbound endpoint URL uses smb://host/share and matches connector endpoint configuration.",
                context);
        }
        catch (OperationCanceledException) when (statusCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _historianLogger.LogWarning("Timed out while reporting {Context}", context);
        }
        catch (Exception ex)
        {
            _historianLogger.LogError(ex, "Failed to report {Context}", context);
        }
    }

    private string NormalizeCronExpression(string cronExpression)
    {
        var trimmed = cronExpression.Trim();
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 5)
        {
            var normalized = $"0 {trimmed}";
            _historianLogger.LogInformation(
                "Converted 5-field cron '{CronExpression}' to 6-field '{NormalizedCron}' for query scheduling",
                trimmed,
                normalized);
            return normalized;
        }

        return trimmed;
    }

    private static bool IsEndpointTopicMismatch(Exception ex)
        => ex.ToString().Contains(EndpointTopicMismatchMessage, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _historianLogger.LogInformation("Historian connector stopping...");

        // Cancel the RQR runner
        _rqrCts?.Cancel();

        if (_rqrRunnerTask != null)
        {
            try
            {
                await _rqrRunnerTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _historianLogger.LogWarning("RQR engine did not stop within timeout");
            }
        }

        // Dispose resources
        _rqrEngine?.Dispose();
        _rqrCts?.Dispose();

        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        _historianLogger.LogInformation("Historian connector stopped");
    }

    private sealed record QueryRuntimeContext
    {
        public required Func<byte[], CancellationToken, Task> ForwardAsync { get; init; }

        public required Func<CancellationToken, Task> ReportAssetHealthyAsync { get; init; }

        public required Func<CancellationToken, Task> ReportDeviceHealthyAsync { get; init; }

        public required Func<string, CancellationToken, Task> ReportDeviceErrorAsync { get; init; }
    }
}
