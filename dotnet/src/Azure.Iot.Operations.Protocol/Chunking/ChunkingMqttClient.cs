// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// MQTT client middleware that provides transparent chunking of large messages.
/// </summary>
public class ChunkingMqttClient : IMqttClient
{
    private readonly IMqttClient _innerClient;
    private readonly ChunkingOptions _chunkingOptions;
    private readonly ConcurrentDictionary<string, ChunkedMessageAssembler> _messageAssemblers = new();
    private readonly ChunkedMessageSplitter _messageSplitter;
    private int _maxPacketSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkingMqttClient"/> class.
    /// </summary>
    /// <param name="innerClient">The MQTT client to wrap with chunking capabilities.</param>
    /// <param name="options">The chunking options.</param>
    public ChunkingMqttClient(IMqttClient innerClient, ChunkingOptions? options = null)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _chunkingOptions = options ?? new ChunkingOptions();
        _messageSplitter = new ChunkedMessageSplitter(_chunkingOptions);

        // Hook into the inner client's event
        _innerClient.ApplicationMessageReceivedAsync += HandleApplicationMessageReceivedAsync;
        _innerClient.ConnectedAsync += HandleConnectedAsync;
        _innerClient.DisconnectedAsync += HandleDisconnectedAsync;
    }

    /// <inheritdoc/>
    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

    /// <inheritdoc/>
    public event Func<MqttClientDisconnectedEventArgs, Task>? DisconnectedAsync;

    /// <inheritdoc/>
    public event Func<MqttClientConnectedEventArgs, Task>? ConnectedAsync;

    /// <inheritdoc/>
    public async Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default)
    {
        var result = await _innerClient.ConnectAsync(options, cancellationToken);

        UpdateMaxPacketSizeFromConnectResult(result);

        return result;
    }

    /// <inheritdoc/>
    public async Task<MqttClientConnectResult> ConnectAsync(MqttConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _innerClient.ConnectAsync(settings, cancellationToken);

        UpdateMaxPacketSizeFromConnectResult(result);

        return result;
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(MqttClientDisconnectOptions? options = null, CancellationToken cancellationToken = default)
    {
        return _innerClient.DisconnectAsync(options, cancellationToken);
    }

    public Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        return _innerClient.ReconnectAsync(cancellationToken);
    }

    public bool IsConnected => _innerClient.IsConnected;

    public Task SendEnhancedAuthenticationExchangeDataAsync(MqttEnhancedAuthenticationExchangeData data, CancellationToken cancellationToken = default)
    {
        return _innerClient.SendEnhancedAuthenticationExchangeDataAsync(data, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default)
    {
        // If chunking is disabled or the message is small enough, pass through to the inner client
        if (!_chunkingOptions.Enabled || applicationMessage.Payload.Length <= Utils.GetMaxChunkSize(_maxPacketSize, _chunkingOptions.StaticOverhead))
        {
            return await _innerClient.PublishAsync(applicationMessage, cancellationToken).ConfigureAwait(false);
        }

        return await PublishChunkedMessageAsync(applicationMessage, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default)
    {
        return _innerClient.SubscribeAsync(options, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default)
    {
        return _innerClient.UnsubscribeAsync(options, cancellationToken);
    }

    public string? ClientId => _innerClient.ClientId;

    public MqttProtocolVersion ProtocolVersion => _innerClient.ProtocolVersion;

    public ValueTask DisposeAsync(bool disposing)
    {
        return _innerClient.DisposeAsync(disposing);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        // Clean up resources
        _messageAssemblers.Clear();

        // Detach events
        _innerClient.ApplicationMessageReceivedAsync -= HandleApplicationMessageReceivedAsync;
        _innerClient.ConnectedAsync -= HandleConnectedAsync;
        _innerClient.DisconnectedAsync -= HandleDisconnectedAsync;

        // Suppress finalization since we're explicitly disposing
        GC.SuppressFinalize(this);

        return _innerClient.DisposeAsync();
    }

    private void UpdateMaxPacketSizeFromConnectResult(MqttClientConnectResult result)
    {
        if (_chunkingOptions.Enabled && result.MaximumPacketSize is not > 0)
        {
            throw new InvalidOperationException("Chunking client requires a defined maximum packet size to function properly.");
        }

        // TODO: @maximsemnov80 figure out how to set the max packet size on the broker side
        // Interlocked.Exchange(ref _maxPacketSize, (int)result.MaximumPacketSize!.Value);
        _maxPacketSize = 64*1024; // 64KB
    }

    private async Task<MqttClientPublishResult> PublishChunkedMessageAsync(MqttApplicationMessage message, CancellationToken cancellationToken)
    {
        // Use the message splitter to split the message into chunks
        var chunks = _messageSplitter.SplitMessage(message, _maxPacketSize);

        // Publish each chunk
        foreach (var chunk in chunks)
        {
            await _innerClient.PublishAsync(chunk, cancellationToken).ConfigureAwait(false);
        }

        // Return a successful result
        return new MqttClientPublishResult(
            null,
            MqttClientPublishReasonCode.Success,
            ChunkingConstants.ChunkedMessageSuccessReasonString,
            new List<MqttUserProperty>(message.UserProperties ?? Enumerable.Empty<MqttUserProperty>()));
    }

    private async Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        // Check if this is a chunked message
        var onApplicationMessageReceivedAsync = ApplicationMessageReceivedAsync;
        if (!TryGetChunkMetadata(args.ApplicationMessage, out var chunkMetadata))
        {
            // Not a chunked message, pass it through
            if (onApplicationMessageReceivedAsync != null)
            {
                await onApplicationMessageReceivedAsync.Invoke(args).ConfigureAwait(false);
            }

            return;
        }

        // This is a chunked message, handle the reassembly
        if (TryProcessChunk(args, chunkMetadata!, out var reassembledArgs))
        {
            // We have a complete message, invoke the event
            if (onApplicationMessageReceivedAsync != null && reassembledArgs != null)
            {
                await onApplicationMessageReceivedAsync.Invoke(reassembledArgs).ConfigureAwait(false);
            }
        }
        else
        {
            // Acknowledge the chunk but don't pass it to the application yet
            await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private bool TryProcessChunk(
        MqttApplicationMessageReceivedEventArgs args,
        ChunkMetadata metadata,
        out MqttApplicationMessageReceivedEventArgs? reassembledArgs)
    {
        reassembledArgs = null;

        // Get or create the message assembler
        var assembler = _messageAssemblers.GetOrAdd(
            metadata.MessageId,
            _ => new ChunkedMessageAssembler(metadata.TotalChunks ?? 0, _chunkingOptions.ChecksumAlgorithm));

        // Add this chunk to the assembler
        if (assembler.AddChunk(metadata.ChunkIndex, args))
        {
            // If this was the first chunk, update total chunks and checksum
            if (metadata.ChunkIndex == 0 && metadata.TotalChunks.HasValue)
            {
                assembler.UpdateMetadata(metadata.TotalChunks.Value, metadata.Checksum);
            }

            // Check if we have all the chunks
            if (assembler.IsComplete && assembler.TryReassemble(out reassembledArgs))
            {
                // Remove the assembler
                _messageAssemblers.TryRemove(metadata.MessageId, out _);
                return true;
            }
        }

        return false;
    }

    private static bool TryGetChunkMetadata(MqttApplicationMessage message, out ChunkMetadata? metadata)
    {
        metadata = null;

        if (message.UserProperties == null)
        {
            return false;
        }

        var chunkProperty = message.UserProperties
            .FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty)
            ?.Value;

        if (string.IsNullOrEmpty(chunkProperty))
        {
            return false;
        }

        try
        {
            metadata = JsonSerializer.Deserialize<ChunkMetadata>(chunkProperty);
            return metadata != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private Task HandleConnectedAsync(MqttClientConnectedEventArgs args)
    {
        // Forward the event
        var handler = ConnectedAsync;
        return handler != null ? handler.Invoke(args) : Task.CompletedTask;
    }

    private Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        // Clear any in-progress reassembly when disconnected
        _messageAssemblers.Clear();

        // Forward the event
        var handler = DisconnectedAsync;
        return handler != null ? handler.Invoke(args) : Task.CompletedTask;
    }
}
