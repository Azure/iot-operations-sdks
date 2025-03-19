// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Converters;
using System.Diagnostics;
using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Mqtt;

/// <summary>
/// A simple pass-through implementation of the IMqttPubSubClient interface that uses MQTTNet as the underlying MQTT client.
/// </summary>
/// <remarks>
/// This client has no built-in retry logic or connection handling. It does add ACK ordering support, though.
/// </remarks>
public class OrderedAckMqttClient : IMqttPubSubClient, IMqttClient
{
    private readonly BlockingConcurrentDelayableQueue<QueuedMqttApplicationMessageReceivedEventArgs> _receivedMessagesToAcknowledgeQueue = new();
    private CancellationTokenSource _acknowledgementSenderTaskCancellationTokenSource = new();
    private Task? _acknowledgementSenderTask;
    private readonly OrderedAckMqttClientOptions _clientOptions;
   
    private TokenRefreshTimer? _tokenRefresh;

    private readonly object _ctsLockObj = new object();

    internal bool _disposed;

    public OrderedAckMqttClient(MQTTnet.IMqttClient mqttNetClient, OrderedAckMqttClientOptions? clientOptions = null)
    {
        _clientOptions = clientOptions ?? new OrderedAckMqttClientOptions();
        UnderlyingMqttClient = mqttNetClient;
                
        UnderlyingMqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
        UnderlyingMqttClient.DisconnectedAsync += OnDisconnectedAsync;
        UnderlyingMqttClient.ConnectedAsync += OnConnectedAsync;

        if (UnderlyingMqttClient.IsConnected)
        {
            StartAcknowledgingReceivedMessages();
        }
    }

    /// <summary>
    /// The MQTT client used by this client to handle all MQTT operations.
    /// </summary>
    public MQTTnet.IMqttClient UnderlyingMqttClient { get; }

    /// <inheritdoc/>
    public string? ClientId => UnderlyingMqttClient.Options?.ClientId;

    /// <inheritdoc/>
    public MqttProtocolVersion ProtocolVersion => (MqttProtocolVersion)((int) UnderlyingMqttClient.Options.ProtocolVersion);

    /// <inheritdoc/>
    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

    /// <summary>
    /// An event that executes every time this client is disconnected.
    /// </summary>
    public event Func<MqttClientDisconnectedEventArgs, Task>? DisconnectedAsync;

    /// <summary>
    /// An event that executes every time this client is connected.
    /// </summary>
    public event Func<MqttClientConnectedEventArgs, Task>? ConnectedAsync;
    private uint _maximumPacketSize;

    public virtual async Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        _maximumPacketSize = options.MaximumPacketSize;
        var mqttNetOptions = MqttNetConverter.FromGeneric(options, UnderlyingMqttClient);

        if (options.AuthenticationMethod == "K8S-SAT")
        {
            // Logs the status codes received by this client in response to re-authentication requests.
            // Note that this cannot be null because MQTTnet's client implementation will close the connection if
            // no handler is set, even if the handler does nothing.
            mqttNetOptions.EnhancedAuthenticationHandler ??= new SatEnhancedAuthenticationHandler();
        }

        if (_clientOptions.EnableAIOBrokerFeatures)
        {
            mqttNetOptions.UserProperties.Add(new("metriccategory", "aiosdk-dotnet"));
        }

        MqttClientConnectResult? result =  MqttNetConverter.ToGeneric(await UnderlyingMqttClient.ConnectAsync(mqttNetOptions, cancellationToken).ConfigureAwait(false));
        if (options.AuthenticationMethod == "K8S-SAT")
        {
            _tokenRefresh = new TokenRefreshTimer(this, options.UserProperties.Where(p => p.Name == "tokenFilePath").First().Value);
        }

        // A successful connect attempt should always return a non-null connect result
        Debug.Assert(result != null);

        if (string.IsNullOrEmpty(UnderlyingMqttClient.Options.ClientId))
        {
            UnderlyingMqttClient.Options.ClientId = result.AssignedClientIdentifier;
        }

        return result;
    }

    /// <summary>
    /// Get the maximum packet size that this client can send.
    /// </summary>
    /// <returns>The maximum packet size.</returns>
     public uint GetMaximumPacketSize()
     {
        return _maximumPacketSize;
     }

    /// <summary>
    /// Connect this client to the MQTT broker configured in the provided connection settings.
    /// </summary>
    /// <param name="settings">The details about the MQTT broker to connect to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The CONNACK returned by the MQTT broker.</returns>
    public virtual Task<MqttClientConnectResult> ConnectAsync(MqttConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ConnectAsync(new MqttClientOptions(settings), cancellationToken);
    }

    public Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        if (UnderlyingMqttClient.Options == null)
        {
            throw new InvalidOperationException("Cannot reconnect prior to the initial connect");
        }

        return ConnectAsync(MqttNetConverter.ToGeneric(UnderlyingMqttClient.Options, UnderlyingMqttClient), cancellationToken);
    }

    /// <summary>
    /// Disconnect this client from the MQTT broker.
    /// </summary>
    /// <param name="options">The optional parameters to include in the DISCONNECT request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual Task DisconnectAsync(MqttClientDisconnectOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_ctsLockObj)
        {
            StopAcknowledgingReceivedMessages();
        }

        return UnderlyingMqttClient.DisconnectAsync(MqttNetConverter.FromGeneric(options ?? new MqttClientDisconnectOptions()), cancellationToken);
    }

    /// <summary>
    /// Validate the size of the message before sending it to the MQTT broker.
    /// </summary>
    /// <param name="message">The message to validate.</param>
    /// <exception cref="InvalidOperationException">If the message size is too large.</exception>
    /// <remarks>
    /// </remarks>
    private Task ValidateMessageSize(MqttApplicationMessage message)
    {
        if (_maximumPacketSize > 0 && message.Payload.Length > _maximumPacketSize)
        {
            throw new InvalidOperationException($"Message size is too large. Maximum message size is {_maximumPacketSize} bytes.");
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual async Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        await ValidateMessageSize(applicationMessage);
        return MqttNetConverter.ToGeneric(await UnderlyingMqttClient.PublishAsync(MqttNetConverter.FromGeneric(applicationMessage), cancellationToken));
    }

    /// <inheritdoc/>
    public virtual async Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return MqttNetConverter.ToGeneric(await UnderlyingMqttClient.SubscribeAsync(MqttNetConverter.FromGeneric(options), cancellationToken));
    }

    /// <inheritdoc/>
    public virtual async Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return MqttNetConverter.ToGeneric(await UnderlyingMqttClient.UnsubscribeAsync(MqttNetConverter.FromGeneric(options), cancellationToken));
    }

    public bool IsConnected => UnderlyingMqttClient.IsConnected;

    private async Task OnMessageReceived(MQTTnet.MqttApplicationMessageReceivedEventArgs mqttNetArgs)
    {
        // Never let MQTTnet auto ack a message because it may do so out-of-order
        mqttNetArgs.AutoAcknowledge = false;
        if (mqttNetArgs.ApplicationMessage.QualityOfServiceLevel == MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
        {
            // QoS 0 messages don't have to worry about ack'ing or ack ordering, so just pass along the event args as-is.
            if (ApplicationMessageReceivedAsync != null)
            {
                try
                {
                    await ApplicationMessageReceivedAsync.Invoke(
                        MqttNetConverter.ToGeneric(
                            mqttNetArgs,
                            (args, cancellationToken) =>
                            {
                                // no ack handler needed since this was a QoS 0 message
                                return Task.CompletedTask;
                            }));
                }
                catch (Exception e)
                {
                    Trace.TraceError("Encountered an exception during the user-supplied callback for handling received messages. {0}", e);
                }
            }
        }
        else
        {
            var queuedArgs = new QueuedMqttApplicationMessageReceivedEventArgs(mqttNetArgs);

            // Create a copy of the received message args, but with an acknowledge handler that simply signals
            // that this message is ready to be acknowledged rather than actually sending the acknowledgement.
            // This is done so that the acknowledgements can be sent in the order that the messages were received in
            // instead of being sent at the moment the user acknowledges them.
            MqttApplicationMessageReceivedEventArgs userFacingMessageReceivedEventArgs = 
                MqttNetConverter.ToGeneric(
                    mqttNetArgs,
                    (args, cancellationToken) =>
                    {
                        queuedArgs.MarkAsReady();
                        _receivedMessagesToAcknowledgeQueue.Signal();
                        return Task.CompletedTask;
                    });

            _receivedMessagesToAcknowledgeQueue.Enqueue(queuedArgs);

            // By default, this client will automatically acknowledge a received message (in the right order)
            userFacingMessageReceivedEventArgs.AutoAcknowledge = true;
            if (ApplicationMessageReceivedAsync != null)
            {
                try
                {
                    // Note that this invocation does need to be awaited because the user is allowed/expected to set the AutoAcknowledge property
                    // on the provided args and the underlying MQTTnet client will auto acknowledge by default.
                    await ApplicationMessageReceivedAsync.Invoke(userFacingMessageReceivedEventArgs);
                }
                catch (Exception e)
                {
                    Trace.TraceError("Encountered an exception during the user-supplied callback for handling received messages. {0}", e);
                    // The user probably didn't get a chance to acknowledge the message, so send the acknowledgement for them.
                }

                // Even if the user wants to auto-acknowledge, we still need to go through the queue's ordering for each acknowledgement.
                // Note that this means the underlying MQTT library will interpret all messages as AutoAcknowledge=false.
                if (userFacingMessageReceivedEventArgs.AutoAcknowledge)
                {
                    queuedArgs.MarkAsReady();
                    _receivedMessagesToAcknowledgeQueue.Signal();
                }
            }
        }
    }

    private Task OnDisconnectedAsync(MQTTnet.MqttClientDisconnectedEventArgs args)
    {
        lock (_ctsLockObj)
        {
            StopAcknowledgingReceivedMessages();
        }

        if (DisconnectedAsync != null)
        {
            _ = DisconnectedAsync.Invoke(MqttNetConverter.ToGeneric(args));
        }

        return Task.CompletedTask;
    }

    private Task OnConnectedAsync(MQTTnet.MqttClientConnectedEventArgs args)
    {
        if (ConnectedAsync != null)
        {
            _ = ConnectedAsync.Invoke(MqttNetConverter.ToGeneric(args));
        }

        StartAcknowledgingReceivedMessages();

        return Task.CompletedTask;
    }

    private async Task PublishAcknowledgementsAsync(CancellationToken connectionLostCancellationToken)
    {
        try
        {
            while (UnderlyingMqttClient.IsConnected)
            {
                // This call will block until there is a first element in the queue and until that first element is ready
                // to be acknowledged. 
                QueuedMqttApplicationMessageReceivedEventArgs queuedArgs = _receivedMessagesToAcknowledgeQueue.Dequeue(connectionLostCancellationToken);

                await queuedArgs.Args.AcknowledgeAsync(connectionLostCancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Trace.TraceInformation("Send acknowledgements task cancelled.");
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Error while sending queued acknowledgements. {0}", exception);
        }
        finally
        {
            Trace.TraceInformation("Stopped sending acknowledgements.");
        }
    }

    private void StartAcknowledgingReceivedMessages()
    {
        if (!_disposed && _acknowledgementSenderTask == null)
        {
            _acknowledgementSenderTask = Task.Run(() => PublishAcknowledgementsAsync(_acknowledgementSenderTaskCancellationTokenSource.Token), _acknowledgementSenderTaskCancellationTokenSource.Token);
        }
    }

    private void StopAcknowledgingReceivedMessages()
    {
        _acknowledgementSenderTaskCancellationTokenSource.Cancel(false);
        _receivedMessagesToAcknowledgeQueue.Clear();
        _acknowledgementSenderTaskCancellationTokenSource?.Dispose();
        _acknowledgementSenderTaskCancellationTokenSource = new();
        _acknowledgementSenderTask = null;
    }

    public virtual async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    public virtual async ValueTask DisposeAsync(bool disposing)
    {
        await DisposeAsyncCore();
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (!_disposed)
        {
            UnderlyingMqttClient.DisconnectedAsync -= OnDisconnectedAsync;
            UnderlyingMqttClient.ConnectedAsync -= OnConnectedAsync;
            
            if (IsConnected) 
            {
                try
                {
                    await DisconnectAsync();
                }
                catch (Exception e)
                {
                    Trace.TraceWarning("Encountered an error while disconnecting during disposal {0}", e);
                }
            }

            _tokenRefresh?.Dispose();

            UnderlyingMqttClient.Dispose();
            _acknowledgementSenderTaskCancellationTokenSource.Dispose();
            _disposed = true;
        }
    }

    public Task SendEnhancedAuthenticationExchangeDataAsync(MqttEnhancedAuthenticationExchangeData data, CancellationToken cancellationToken = default)
    {
        return UnderlyingMqttClient.SendEnhancedAuthenticationExchangeDataAsync(MqttNetConverter.FromGeneric(data), cancellationToken);
    }
}
