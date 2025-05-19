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
    private readonly ChunkingOptions _options;
    private readonly ConcurrentDictionary<string, ChunkedMessageAssembler> _messageAssemblers = new();
    private int _maxPacketSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkingMqttClient"/> class.
    /// </summary>
    /// <param name="innerClient">The MQTT client to wrap with chunking capabilities.</param>
    /// <param name="options">The chunking options.</param>
    public ChunkingMqttClient(IMqttClient innerClient, ChunkingOptions? options = null)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _options = options ?? new ChunkingOptions();

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
        var result = await _innerClient.ConnectAsync(options, cancellationToken).ConfigureAwait(false);

        if (!result.MaximumPacketSize.HasValue)
        {
            throw new InvalidOperationException("Chunking client requires a defined maximum packet size to function properly.");
        }

        _maxPacketSize = (int)result.MaximumPacketSize.Value;
        return result;
    }

    /// <inheritdoc/>
    public async Task<MqttClientConnectResult> ConnectAsync(MqttConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _innerClient.ConnectAsync(settings, cancellationToken).ConfigureAwait(false);

        if (!result.MaximumPacketSize.HasValue)
        {
            throw new InvalidOperationException("Chunking client requires a defined maximum packet size to function properly.");
        }

        _maxPacketSize = (int)result.MaximumPacketSize;
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
        if (!_options.Enabled || applicationMessage.Payload.Length <= GetMaxChunkSize())
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

    private int GetMaxChunkSize()
    {
        // Subtract the static overhead to ensure we don't exceed the broker's limits
        return Math.Max(0, _maxPacketSize - _options.StaticOverhead);
    }

    private async Task<MqttClientPublishResult> PublishChunkedMessageAsync(MqttApplicationMessage message, CancellationToken cancellationToken)
    {
        var maxChunkSize = GetMaxChunkSize();
        var payload = message.Payload;
        var totalChunks = (int)Math.Ceiling((double)payload.Length / maxChunkSize);

        // Generate a unique message ID
        var messageId = Guid.NewGuid().ToString("D");

        // Calculate checksum for the entire payload
        var checksum = ChecksumCalculator.CalculateChecksum(payload, _options.ChecksumAlgorithm);

        // Create a copy of the user properties
        var userProperties = new List<MqttUserProperty>(message.UserProperties ?? Enumerable.Empty<MqttUserProperty>());

        // Send each chunk
        for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
        {
            // Create chunk metadata
            var metadata = chunkIndex == 0
                ? ChunkMetadata.CreateFirstChunk(messageId, totalChunks, checksum, _options.ChunkTimeout)
                : ChunkMetadata.CreateSubsequentChunk(messageId, chunkIndex, _options.ChunkTimeout);

            // Serialize the metadata to JSON
            var metadataJson = JsonSerializer.Serialize(metadata);

            // Create user properties for this chunk
            var chunkUserProperties = new List<MqttUserProperty>(userProperties)
            {
                // Add the chunk metadata property
                new(ChunkingConstants.ChunkUserProperty, metadataJson)
            };

            // Extract the chunk payload
            var chunkStart = (long)chunkIndex * maxChunkSize;
            var chunkLength = Math.Min(maxChunkSize, payload.Length - chunkStart);
            var chunkPayload = payload.Slice(chunkStart, chunkLength);

            // Create a message for this chunk
            var chunkMessage = new MqttApplicationMessage(message.Topic, message.QualityOfServiceLevel)
            {
                Retain = message.Retain,
                Payload = chunkPayload,
                ContentType = message.ContentType,
                ResponseTopic = message.ResponseTopic,
                CorrelationData = message.CorrelationData,
                PayloadFormatIndicator = message.PayloadFormatIndicator,
                MessageExpiryInterval = message.MessageExpiryInterval,
                TopicAlias = message.TopicAlias,
                SubscriptionIdentifiers = message.SubscriptionIdentifiers,
                UserProperties = chunkUserProperties
            };

            // Publish the chunk
            await _innerClient.PublishAsync(chunkMessage, cancellationToken).ConfigureAwait(false);
        }

        // Return a successful result
        return new MqttClientPublishResult(
            null,
            MqttClientPublishReasonCode.Success,
            string.Empty,
            new List<MqttUserProperty>(message.UserProperties ?? Enumerable.Empty<MqttUserProperty>()));
    }

    private async Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        // Check if this is a chunked message
        var chunkMetadata = TryGetChunkMetadata(args.ApplicationMessage);

        if (chunkMetadata == null)
        {
            // Not a chunked message, pass it through
            if (ApplicationMessageReceivedAsync != null)
            {
                await ApplicationMessageReceivedAsync.Invoke(args).ConfigureAwait(false);
            }

            return;
        }

        // This is a chunked message, handle the reassembly
        if (TryProcessChunk(args, chunkMetadata, out var reassembledArgs))
        {
            // We have a complete message, invoke the event
            if (ApplicationMessageReceivedAsync != null && reassembledArgs != null)
            {
                await ApplicationMessageReceivedAsync.Invoke(reassembledArgs).ConfigureAwait(false);
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
            _ => new ChunkedMessageAssembler(metadata.TotalChunks ?? 0, _options.ChecksumAlgorithm));

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

    private static ChunkMetadata? TryGetChunkMetadata(MqttApplicationMessage message)
    {
        if (message.UserProperties == null)
        {
            return null;
        }

        var chunkProperty = message.UserProperties
            .FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty)
            ?.Value;

        if (string.IsNullOrEmpty(chunkProperty))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ChunkMetadata>(chunkProperty);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Task HandleConnectedAsync(MqttClientConnectedEventArgs args)
    {
        if (!args.ConnectResult.MaximumPacketSize.HasValue)
        {
            throw new InvalidOperationException("Chunking client requires a defined maximum packet size to function properly.");
        }

        _maxPacketSize = (int)args.ConnectResult.MaximumPacketSize.Value;

        // Forward the event
        return ConnectedAsync?.Invoke(args) ?? Task.CompletedTask;
    }

    private Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        // Clear any in-progress reassembly when disconnected
        _messageAssemblers.Clear();

        // Forward the event
        return DisconnectedAsync?.Invoke(args) ?? Task.CompletedTask;
    }
}
