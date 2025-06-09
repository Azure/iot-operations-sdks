// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
public class ChunkingMqttPubSubClient : IMqttPubSubClient
{
    private readonly IExtendedPubSubMqttClient _innerClient;
    private readonly ChunkingOptions _chunkingOptions;
    private readonly ConcurrentDictionary<string, ChunkedMessageAssembler> _messageAssemblers = new();
    private readonly ChunkedMessageSplitter _messageSplitter;
    private int _maxPacketSize;
    private readonly Timer? _cleanupTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkingMqttPubSubClient"/> class.
    /// </summary>
    /// <param name="innerClient">The MQTT client to wrap with chunking capabilities.</param>
    /// <param name="options">The chunking options.</param>
    public ChunkingMqttPubSubClient(IExtendedPubSubMqttClient innerClient, ChunkingOptions? options = null)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _chunkingOptions = options ?? new ChunkingOptions();
        _messageSplitter = new ChunkedMessageSplitter(_chunkingOptions);

        UpdateMaxPacketSizeFromConnectResult(_innerClient.GetConnectResult());

        _innerClient.ApplicationMessageReceivedAsync += HandleApplicationMessageReceivedAsync;

        // Start the cleanup timer
        _cleanupTimer = new Timer(
            _ => CleanupExpiredAssemblers(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

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

        // Dispose cleanup timer
        _cleanupTimer?.Dispose();

        // Detach events
        _innerClient.ApplicationMessageReceivedAsync -= HandleApplicationMessageReceivedAsync;

        // Suppress finalization since we're explicitly disposing
        GC.SuppressFinalize(this);

        return _innerClient.DisposeAsync();
    }

    private void UpdateMaxPacketSizeFromConnectResult(MqttClientConnectResult? result)
    {
        if (_chunkingOptions.Enabled && result?.MaximumPacketSize is not > 0)
        {
            throw new InvalidOperationException("Chunking client requires a defined maximum packet size to function properly.");
        }

        // _maxPacketSize = (int)result!.MaximumPacketSize!.Value;
        _maxPacketSize = 64 * 1024;
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
            // If this was the first chunk, update total chunks, checksum, and extract timeout from MessageExpiryInterval
            if (metadata.ChunkIndex == 0 && metadata.TotalChunks.HasValue)
            {
                var timeout = args.ApplicationMessage.MessageExpiryInterval > 0
                    ? TimeSpan.FromSeconds(args.ApplicationMessage.MessageExpiryInterval)
                    : (TimeSpan?)null;
                assembler.UpdateMetadata(metadata.TotalChunks.Value, metadata.Checksum, timeout);
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

    /// <summary>
    /// Cleans up expired message assemblers to prevent memory leaks.
    /// </summary>
    private void CleanupExpiredAssemblers()
    {
        var expiredKeys = new List<string>();

        foreach (var kvp in _messageAssemblers)
        {
            if (kvp.Value.HasExpired())
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            _messageAssemblers.TryRemove(key, out _);
        }
    }
}
