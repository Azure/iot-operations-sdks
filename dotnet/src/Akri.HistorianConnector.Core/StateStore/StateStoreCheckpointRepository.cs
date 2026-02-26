// <copyright file="StateStoreCheckpointRepository.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.HistorianConnector.Core.Models;
using Azure.Iot.Operations.Protocol;
using Microsoft.Extensions.Logging;

namespace Akri.HistorianConnector.Core.StateStore;

/// <summary>
/// Stores historian query checkpoints in the AIO State Store.
/// </summary>
public sealed class StateStoreCheckpointRepository : IAsyncDisposable
{
    private readonly WatermarkStore<HistorianCheckpoint> _repository;
    private readonly string _connectorInstanceId;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateStoreCheckpointRepository"/> class.
    /// </summary>
    public StateStoreCheckpointRepository(
        ApplicationContext applicationContext,
        IMqttClient mqttClient,
        string connectorInstanceId,
        ILogger<StateStoreCheckpointRepository> logger,
        ILoggerFactory? loggerFactory = null)
    {
        _connectorInstanceId = connectorInstanceId;

        // Use the WatermarkStore's own logger category for proper filtering
        var watermarkLogger = loggerFactory?.CreateLogger<WatermarkStore<HistorianCheckpoint>>()
            ?? (ILogger)logger;

        _repository = new WatermarkStore<HistorianCheckpoint>(
            applicationContext,
            mqttClient,
            $"historian/checkpoints/{connectorInstanceId}",
            watermarkLogger);
    }

    /// <summary>
    /// Gets the checkpoint for a query.
    /// </summary>
    /// <param name="queryId">The query ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checkpoint, or null if not found.</returns>
    public async Task<HistorianCheckpoint?> GetAsync(string queryId, CancellationToken cancellationToken)
    {
        var key = queryId.Replace("/", "_");
        return await _repository.GetAsync(key);
    }

    /// <summary>
    /// Saves a checkpoint for a query.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveAsync(HistorianCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        var key = checkpoint.QueryId.Replace("/", "_");
        await _repository.SetAsync(key, checkpoint);
    }

    /// <summary>
    /// Creates an updated checkpoint after a successful run.
    /// </summary>
    public HistorianCheckpoint CreateSuccessCheckpoint(
        string queryId,
        HistorianCheckpoint.CheckpointWatermarkKind watermarkKind,
        DateTimeOffset? newWatermarkUtc,
        long? newWatermarkSequence,
        HistorianCheckpoint? existing)
    {
        return new HistorianCheckpoint
        {
            QueryId = queryId,
            WatermarkKind = watermarkKind,
            WatermarkUtc = newWatermarkUtc,
            WatermarkSequence = newWatermarkSequence,
            UpdatedUtc = DateTimeOffset.UtcNow,
            ConnectorInstanceId = _connectorInstanceId,
            SuccessfulRunCount = (existing?.SuccessfulRunCount ?? 0) + 1,
            FailedRunCount = existing?.FailedRunCount ?? 0,
            LastError = null,
            LastErrorUtc = null
        };
    }

    /// <summary>
    /// Creates an updated checkpoint after a failed run.
    /// </summary>
    public HistorianCheckpoint CreateFailureCheckpoint(
        string queryId,
        HistorianCheckpoint.CheckpointWatermarkKind watermarkKind,
        string errorMessage,
        HistorianCheckpoint? existing)
    {
        return new HistorianCheckpoint
        {
            QueryId = queryId,
            WatermarkKind = watermarkKind,
            WatermarkUtc = existing?.WatermarkUtc,
            WatermarkSequence = existing?.WatermarkSequence,
            UpdatedUtc = DateTimeOffset.UtcNow,
            ConnectorInstanceId = _connectorInstanceId,
            SuccessfulRunCount = existing?.SuccessfulRunCount ?? 0,
            FailedRunCount = (existing?.FailedRunCount ?? 0) + 1,
            LastError = errorMessage,
            LastErrorUtc = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _repository.DisposeAsync();
    }
}
