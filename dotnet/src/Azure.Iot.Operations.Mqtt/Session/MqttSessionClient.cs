// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net.Sockets;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Converters;
using Azure.Iot.Operations.Mqtt.Session.Exceptions;
using Azure.Iot.Operations.Protocol.Retry;

namespace Azure.Iot.Operations.Mqtt.Session
{
    public class MqttSessionClient : OrderedAckMqttClient
    {
        private readonly MqttSessionClientOptions _sessionClientOptions;

        // "Worker threads" are the threads responsible for polling for enqueued publishes, subscribes, and unsubscribes
        private CancellationTokenSource _workerThreadsTaskCancellationTokenSource = new();

        // publish/subscribe/unsubscribe requests that haven't been fulfilled yet. Some may be in flight, though.
        private readonly BlockingConcurrentList _outgoingRequestList;

        private object ctsLockObj = new();

        private bool _isDesiredConnected;
        private bool _isClosing;
        private CancellationTokenSource? _reconnectionCancellationToken;

        private SemaphoreSlim disconnectedEventLock = new(1);

        public event Func<MqttClientDisconnectedEventArgs, Task>? SessionLostAsync;

        /// <summary>
        /// Create a MQTT session client where the underlying MQTT client is created for you and the connection is maintained
        /// for you.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When an MQTT session client is constructed with this constructor, it will automatically recover the connection
        /// and all previous subscriptions if it detects that the previous connection was lost.
        /// It will also enqueue publishes/subscribes/unsubscribes and send them when the connection is alive.
        /// </para>
        /// <para>
        /// An MQTT session client created with this constructor will only report connection loss and/or publish/subscribe/unsubscribe
        /// failures if they are deemed fatal or if the provided retry policy is exhausted. All transient failures will cause the
        /// retry policy to be checked, but won't cause the <see cref="DisconnectedAsync"/> event to fire.
        /// </para>
        /// </remarks>
        /// <param name="connectionSettings">The configurable options for the underlying MQTT connection(s)</param>
        /// <param name="sessionClientOptions">The configurable options for this MQTT session client.</param>
        public MqttSessionClient(MqttSessionClientOptions? sessionClientOptions = null)
            : base(sessionClientOptions != null && sessionClientOptions.EnableMqttLogging
                  ? new MQTTnet.MqttFactory().CreateMqttClient(MqttNetTraceLogger.CreateTraceLogger())
                  : new MQTTnet.MqttFactory().CreateMqttClient())
        {
            _sessionClientOptions = sessionClientOptions != null ? sessionClientOptions : new MqttSessionClientOptions();
            _sessionClientOptions.Validate();

            DisconnectedAsync += InternalDisconnectedAsync;

            _outgoingRequestList = new(_sessionClientOptions.MaxPendingMessages, _sessionClientOptions.PendingMessagesOverflowStrategy);
        }

        // For unit test purposes only
        internal MqttSessionClient(MQTTnet.Client.IMqttClient mqttClient, MqttSessionClientOptions? sessionClientOptions = null)
            : base(mqttClient)
        {
            _sessionClientOptions = sessionClientOptions != null ? sessionClientOptions : new MqttSessionClientOptions();
            _sessionClientOptions.Validate();

            DisconnectedAsync += InternalDisconnectedAsync;

            _outgoingRequestList = new(_sessionClientOptions.MaxPendingMessages, _sessionClientOptions.PendingMessagesOverflowStrategy);
        }

        /// <summary>
        /// Connect this client and start a clean MQTT session. Once connected, this client will automatically reconnect
        /// as needed and recover the MQTT session.
        /// </summary>
        /// <param name="options">The details about how to connect to the MQTT broker.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The CONNACK received from the MQTT broker.</returns>
        /// <remarks>
        /// This operation does not retry by default, but can be configured to retry. To do so, set the 
        /// <see cref="MqttSessionClientOptions.RetryOnFirstConnect"/> flag and optionally configure the retry policy
        /// via <see cref="MqttSessionClientOptions.ConnectionRetryPolicy"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">If this method is called when the client is already managing the connection.</exception>
        public override async Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default)
        {
            //TODO once the session client is fully integrated into RPC/Telemetry tests, the default session expiry interval should be 0
            // so that non-session client applications don't create sessions unknowingly.

            ObjectDisposedException.ThrowIf(_disposed, this);

            cancellationToken.ThrowIfCancellationRequested();

            if (_isDesiredConnected)
            {
                // should this just return "OK"? Or null since no CONNACK was received?
                throw new InvalidOperationException("The client is already managing the connection.");
            }

            if (options != null && options.SessionExpiryInterval < 1)
            {
                // This client relies on creating an MQTT session that lasts longer than the initial connection. Otherwise all
                // reconnection attempts will fail to resume the session because the broker already expired it.
                throw new ArgumentException("Session expiry interval must be greater than 0.");
            }

            ArgumentNullException.ThrowIfNull(options);

            _isClosing = false;
            MqttClientConnectResult? connectResult = await MaintainConnectionAsync(options, null, cancellationToken);

            // By design, MaintainConnectionAsync should only return null when called during reconnection.
            // When called by this method, MaintainConnectionAsync should return a non-null value or throw.
            Debug.Assert(connectResult != null);
            _isDesiredConnected = true;
            Trace.TraceInformation("Successfully connected the session client to the MQTT broker. This connection will now be maintained.");

            return connectResult;
        }

        /// <summary>
        /// Connect this client and start a clean MQTT session. Once connected, this client will automatically reconnect
        /// as needed and recover the MQTT session.
        /// </summary>
        /// <param name="settings">The details about how to connect to the MQTT broker.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The CONNACK received from the MQTT broker.</returns>
        /// <remarks>
        /// This operation does not retry by default, but can be configured to retry. To do so, set the 
        /// <see cref="MqttSessionClientOptions.RetryOnFirstConnect"/> flag and optionally configure the retry policy
        /// via <see cref="MqttSessionClientOptions.ConnectionRetryPolicy"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">If this method is called when the client is already managing the connection.</exception>
        public override Task<MqttClientConnectResult> ConnectAsync(MqttConnectionSettings settings, CancellationToken cancellationToken = default)
            => ConnectAsync(new MqttClientOptions(settings), cancellationToken);

        /// <summary>
        /// Disconnect this client and end the MQTT session.
        /// </summary>
        /// <param name="options">The optional parameters that can be sent in the DISCONNECT packet to the MQTT broker.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public override async Task DisconnectAsync(MqttClientDisconnectOptions? options = null, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (options != null && options.SessionExpiryInterval != 0)
            {
                // This method should only be called when the session is no longer needed. By providing a non-zero value, you are trying
                // to keep the session alive on the broker.
                throw new ArgumentException("Cannot use a non-zero session expiry interval");
            }

            options ??= new MqttClientDisconnectOptions();
            options.SessionExpiryInterval = 0;

            _isClosing = true;
            _reconnectionCancellationToken?.Cancel();
            await base.DisconnectAsync(options, cancellationToken);

            var disconnectedArgs = new MqttClientDisconnectedEventArgs(
                true,
                null,
                MqttClientDisconnectReason.NormalDisconnection,
                null,
                null,
                null);

            MqttSessionExpiredException e = new("The queued request cannot be completed now that the session client has been closed by the user.");
            await FinalizeSessionAsync(e, disconnectedArgs, cancellationToken);
            StopPublishingSubscribingAndUnsubscribing();
            Trace.TraceInformation("Successfully disconnected the session client from the MQTT broker. This connection will no longer be maintained.");
        }

        /// <summary>
        /// Publish an MQTT message to the MQTT broker.
        /// </summary>
        /// <param name="applicationMessage">The message to publish.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The PUBACK received from the MQTT broker.</returns>
        /// <remarks>
        /// If this operation is interrupted by a connection loss, this client will automatically re-send it once
        /// the client has recovered the connection.
        /// 
        /// This method may be called even when this client is not connected. The request will be sent once the
        /// connection is established.
        /// </remarks>
        public override async Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource<MqttClientPublishResult> tcs = new TaskCompletionSource<MqttClientPublishResult>();

            var queuedRequest = new QueuedPublishRequest(applicationMessage, tcs, cancellationToken: cancellationToken);

            cancellationToken.Register(async () =>
            {
                try
                {
                    await _outgoingRequestList.RemoveAsync(queuedRequest, CancellationToken.None);
                    tcs.TrySetCanceled();
                }
                catch (ObjectDisposedException)
                {
                    Trace.TraceWarning("Failed to remove a queued publish because the session client was already disposed.");
                }
            });

            await _outgoingRequestList.AddLastAsync(
                queuedRequest,
                cancellationToken);

            MqttClientPublishResult publishResult = await tcs.Task;

            return publishResult;
        }

        /// <summary>
        /// Send a SUBSCRIBE request to the MQTT broker.
        /// </summary>
        /// <param name="options">The details of the subscribe request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The SUBACK received from the MQTT broker.</returns>
        /// <remarks>
        /// If this operation is interrupted by a connection loss, this client will automatically re-send it once
        /// the client has recovered the connection.
        /// 
        /// This method may be called even when this client is not connected. The request will be sent once the
        /// connection is established.
        /// </remarks>
        public override async Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource<MqttClientSubscribeResult> tcs = new TaskCompletionSource<MqttClientSubscribeResult>();

            var queuedRequest = new QueuedSubscribeRequest(options, tcs, cancellationToken: cancellationToken);
            cancellationToken.Register(async () =>
            {
                try
                {
                    await _outgoingRequestList.RemoveAsync(queuedRequest, CancellationToken.None);
                    tcs.TrySetCanceled();
                }
                catch (ObjectDisposedException)
                {
                    Trace.TraceWarning("Failed to remove a queued subscribe because the session client was already disposed.");
                }
            });

            await _outgoingRequestList.AddLastAsync(
                queuedRequest,
                cancellationToken);

            MqttClientSubscribeResult result = await tcs.Task;

            return result;
        }

        /// <summary>
        /// Send a UNSUBSCRIBE request to the MQTT broker.
        /// </summary>
        /// <param name="options">The details of the unsubscribe request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The UNSUBACK received from the MQTT broker.</returns>
        /// <remarks>
        /// If this operation is interrupted by a connection loss, this client will automatically re-send it once
        /// the client has recovered the connection.
        /// 
        /// This method may be called even when this client is not connected. The request will be sent once the
        /// connection is established.
        /// </remarks>
        public override async Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource<MqttClientUnsubscribeResult> tcs = new TaskCompletionSource<MqttClientUnsubscribeResult>();

            var queuedRequest = new QueuedUnsubscribeRequest(options, tcs, cancellationToken: cancellationToken);
            cancellationToken.Register(async () =>
            {
                try
                {
                    await _outgoingRequestList.RemoveAsync(queuedRequest, CancellationToken.None);
                    tcs.TrySetCanceled();
                }
                catch (ObjectDisposedException)
                {
                    Trace.TraceWarning("Failed to remove a queued unsubscribe because the session client was already disposed.");
                }
            });

            await _outgoingRequestList.AddLastAsync(
                queuedRequest,
                cancellationToken);

            MqttClientUnsubscribeResult result = await tcs.Task;

            return result;
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                DisconnectedAsync -= InternalDisconnectedAsync;

                if (IsConnected || _isDesiredConnected)
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

                _workerThreadsTaskCancellationTokenSource?.Dispose();
                _reconnectionCancellationToken?.Dispose();
                disconnectedEventLock.Dispose();
                _outgoingRequestList.Dispose();
            }

            _workerThreadsTaskCancellationTokenSource?.Dispose();
            disconnectedEventLock.Dispose();
            _outgoingRequestList.Dispose();

            // The underlying client has an MQTT client as a managed resource that no other client has access to, so always dispose it
            // alongside all unmanaged resources.
            await base.DisposeAsync(true);

            GC.SuppressFinalize(this);
            await base.DisposeAsync();
        }

        private async Task InternalDisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            // MQTTNet's client often triggers the same "OnDisconnect" callback more times than expected, so only start reconnection once
            await disconnectedEventLock.WaitAsync();

            try
            {
                if (_isDesiredConnected)
                {
                    if (base.IsConnected)
                    {
                        Trace.TraceInformation("Disconnect reported by MQTTnet client, but it was already handled");
                        return;
                    }

                    StopPublishingSubscribingAndUnsubscribing();

                    // It is important to stop the pub/sub/unsub threads before resetting these message states.
                    // If you reset the message states first, then pubs/subs/unsubs may be dequeued into about-to-be-cancelled threads.
                    await ResetMessagesStates(default);

                    if (IsFatal(args.Reason))
                    {
                        Trace.TraceInformation("Disconnect detected and it was due to fatal error. The client will not attempt to reconnect. Disconnect reason: {0}", args.Reason);
                        var retryException = new RetryExpiredException("A fatal error was encountered while trying to re-establish the session, so this request cannot be completed.", args.Exception);
                        await FinalizeSessionAsync(retryException, args, CancellationToken.None);
                        return;
                    }

                    Trace.TraceInformation("Disconnect detected, starting reconnection. Disconnect reason: {0}", args.Reason);

                    var options = MqttNetConverter.ToGeneric(UnderlyingMqttClient.Options, UnderlyingMqttClient);

                    // This field is set when connecting, and this function should only be called after connecting.
                    Debug.Assert(options != null);

                    _reconnectionCancellationToken?.Dispose();
                    _reconnectionCancellationToken = new();

                    // start reconnection if the user didn't initiate this disconnect
                    await MaintainConnectionAsync(options, args, _reconnectionCancellationToken.Token);
                }
            }
            finally
            {
                disconnectedEventLock.Release();
            }
        }

        private async Task<MqttClientConnectResult?> MaintainConnectionAsync(MqttClientOptions options, MqttClientDisconnectedEventArgs? lastDisconnect, CancellationToken cancellationToken)
        {
            // This function is either called when initially connecting the client or when reconnecting it. The behavior
            // of this function should change depending on the context it was called. For instance, thrown exceptions are the expected
            // behavior when called from the initial ConnectAsync thread, but any exceptions thrown in the reconnection thread will be
            // unhandled and may crash the client.
            bool isReconnection = lastDisconnect != null;
            uint attemptCount = 1;
            MqttClientConnectResult? mostRecentConnectResult = null;
            Exception? lastException = lastDisconnect?.Exception;
            TimeSpan retryDelay = TimeSpan.Zero;

            while (true)
            {
                // This flag signals that the user is trying to close the connection. If this happens when the client is reconnection,
                // simply abandon reconnecting and end this task.
                if (_isClosing && isReconnection)
                {
                    return null;
                }
                else if (_isClosing && lastException != null)
                {
                    // If the user disconnects the client while they were trying to connect it,
                    // stop trying to connect it and just report the most recent error.
                    throw lastException;
                }

                if (!isReconnection && attemptCount > 1 && !_sessionClientOptions.RetryOnFirstConnect)
                {
                    Debug.Assert(lastException != null);
                    throw lastException;
                }

                if (IsFatal(lastException!, isReconnection, _reconnectionCancellationToken?.Token.IsCancellationRequested ?? cancellationToken.IsCancellationRequested))
                {
                    Trace.TraceError("Encountered a fatal exception while maintaining connection {0}", lastException);
                    if (isReconnection)
                    {
                        var retryException = new RetryExpiredException("A fatal error was encountered while trying to re-establish the session, so this request cannot be completed.", lastException);

                        // This function was called to reconnect after an unexpected disconnect. Since the error is fatal,
                        // notify the user via callback that the client has crashed, but don't throw the exception since
                        // this task is unmonitored.
                        await FinalizeSessionAsync(retryException, lastDisconnect!, cancellationToken);
                        return null;
                    }
                    else
                    {
                        // This function was called directly by the user via ConnectAsync, so just throw the exception.
                        throw lastException!;
                    }
                }

                // Always consult the retry policy when reconnecting, but only consult it on attempt > 1 when
                // initially connecting
                if ((isReconnection || attemptCount > 1)
                    && !_sessionClientOptions.ConnectionRetryPolicy.ShouldRetry(attemptCount, lastException!, out retryDelay))
                {
                    Trace.TraceError("Retry policy was exhausted while trying to maintain a connection {0}", lastException);
                    var retryException = new RetryExpiredException("Retry policy has been exhausted. See inner exception for the latest exception encountered while retrying.", lastException);

                    if (lastDisconnect != null)
                    {
                        // This function was called to reconnect after an unexpected disconnect. Since the error is fatal,
                        // notify the user via callback that the client has crashed, but don't throw the exception since
                        // this task is unmonitored.
                        var disconnectedEventArgs = new MqttClientDisconnectedEventArgs(
                            lastDisconnect.ClientWasConnected,
                            mostRecentConnectResult,
                            lastDisconnect.Reason,
                            lastDisconnect.ReasonString,
                            lastDisconnect.UserProperties,
                            retryException);

                        await FinalizeSessionAsync(retryException, disconnectedEventArgs, cancellationToken);
                        return null;
                    }
                    else
                    {
                        // This function was called directly by the user via ConnectAsync, so just throw the exception.
                        throw retryException;
                    }
                }

                // With all the above conditions checked, the client should attempt to connect again after a delay
                try
                {
                    if (retryDelay.CompareTo(TimeSpan.Zero) > 0)
                    {
                        Trace.TraceInformation("Waiting {0} before next reconnect attempt", retryDelay);
                        await Task.Delay(retryDelay, cancellationToken);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    Trace.TraceInformation($"Trying to connect. Attempt number {attemptCount}");

                    if (isReconnection || _sessionClientOptions.RetryOnFirstConnect)
                    {
                        using CancellationTokenSource reconnectionTimeoutCancellationToken = new();
                        reconnectionTimeoutCancellationToken.CancelAfter(_sessionClientOptions.ConnectionAttemptTimeout);
                        using CancellationTokenSource linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, reconnectionTimeoutCancellationToken.Token);
                        mostRecentConnectResult = await TryEstablishConnectionAsync(options, linkedCancellationToken.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        mostRecentConnectResult = await TryEstablishConnectionAsync(options, cancellationToken).ConfigureAwait(false);
                    }

                    // If the connection was re-established, but the session was lost, report it as a fatal error to the user and disconnect from the broker.
                    if (mostRecentConnectResult != null
                        && mostRecentConnectResult.ResultCode == MqttClientConnectResultCode.Success
                        && isReconnection
                        && !mostRecentConnectResult.IsSessionPresent)
                    {
                        var disconnectedArgs = new MqttClientDisconnectedEventArgs(
                            true,
                            null,
                            MqttClientDisconnectReason.NormalDisconnection,
                            "The session client re-established the connection, but the MQTT broker no longer had the session.",
                            null,
                            new MqttSessionExpiredException());

                        MqttSessionExpiredException queuedItemException = new("The queued request has been cancelled because the session is no longer present");
                        await FinalizeSessionAsync(queuedItemException, disconnectedArgs, cancellationToken);
                        Trace.TraceError("Reconnection succeeded, but the session was lost so the client closed the connection.");

                        await FinalizeSessionAsync(queuedItemException, disconnectedArgs, cancellationToken);
                        // The provided cancellation token will be cancelled while disconnecting, so don't pass it along
                        await DisconnectAsync(null, CancellationToken.None).ConfigureAwait(false);

                        // Reconnection should end because the session was lost
                        return null;
                    }

                    if (isReconnection)
                    {
                        Trace.TraceInformation("Reconnection finished after successfully connecting to the MQTT broker again and re-joining the existing MQTT session.");
                    }

                    if (mostRecentConnectResult != null
                        && mostRecentConnectResult.ResultCode == MqttClientConnectResultCode.Success)
                    {
                        StartPublishingSubscribingAndUnsubscribing();
                    }

                    return mostRecentConnectResult;
                }
                catch (Exception) when (_isClosing && isReconnection)
                {
                    // This happens when reconnecting if the user attempts to manually disconnect the session client. When
                    // that happens, we simply want to end the reconnection logic and let the thread end without throwing.
                    Trace.TraceInformation("Session client reconnection cancelled because the client is being closed.");
                    return null;
                }
                catch (Exception e)
                {
                    lastException = e;
                    Trace.TraceWarning($"Encountered an exception while connecting. May attempt to reconnect. {e}");
                }

                attemptCount++;
            }
        }

        private async Task<MqttClientConnectResult?> TryEstablishConnectionAsync(MqttClientOptions options, CancellationToken cancellationToken)
        {
            if (IsConnected)
            {
                return null;
            }

            // When reconnecting, never use a clean session. This client wants to recover the session and the connection.
            if (_isDesiredConnected)
            {
                options.CleanSession = false;
            }

            MqttClientConnectResult? connectResult =
                await base.ConnectAsync(options, cancellationToken).ConfigureAwait(false);

            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new MqttConnectingFailedException($"Client tried to connect but server denied connection with reason '{connectResult.ResultCode}'.", connectResult);
            }

            return connectResult;
        }

        private void StartPublishingSubscribingAndUnsubscribing()
        {
            lock (ctsLockObj)
            {
                if (!_disposed)
                {
                    Trace.TraceInformation("Starting the session client's worker thread");
                    _ = Task.Run(() => ExecuteQueuedItemsAsync(_workerThreadsTaskCancellationTokenSource.Token), _workerThreadsTaskCancellationTokenSource.Token);
                }
            }
        }

        private void StopPublishingSubscribingAndUnsubscribing()
        {
            lock (ctsLockObj)
            {
                try
                {
                    Trace.TraceInformation("Stopping the session client's worker thread");
                    _workerThreadsTaskCancellationTokenSource.Cancel(false);
                    _workerThreadsTaskCancellationTokenSource.Dispose();
                    _workerThreadsTaskCancellationTokenSource = new();
                }
                catch (ObjectDisposedException)
                {
                    // The object was already disposed prior to this method being called
                }
            }
        }

        private async Task ResetMessagesStates(CancellationToken cancellationToken)
        {
            Trace.TraceInformation("Resetting the state of all queued messages");
            await _outgoingRequestList.MarkMessagesAsUnsent(cancellationToken);
        }

        private async Task ExecuteQueuedItemsAsync(CancellationToken connectionLostCancellationToken)
        {
            try
            {
                while (base.IsConnected)
                {
                    QueuedRequest queuedRequest = await _outgoingRequestList.PeekNextUnsentAsync(connectionLostCancellationToken);
                    connectionLostCancellationToken.ThrowIfCancellationRequested();

                    // This request can either be cancelled because the connection was lost or because the user cancelled this specific request
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(connectionLostCancellationToken, queuedRequest.CancellationToken);

                    if (queuedRequest is QueuedPublishRequest queuedPublishRequest)
                    {
                        _ = ExecuteSinglePublishAsync(queuedPublishRequest, cts.Token);
                    }
                    else if (queuedRequest is QueuedSubscribeRequest queuedSubscribeRequest)
                    {
                        _ = ExecuteSingleSubscribeAsync(queuedSubscribeRequest, cts.Token);
                    }
                    else if (queuedRequest is QueuedUnsubscribeRequest queuedUnsubscribeRequest)
                    {
                        _ = ExecuteSingleUnsubscribeAsync(queuedUnsubscribeRequest, cts.Token);
                    }
                    else
                    {
                        // This should never happen since the queue should only contain pubs, subs, and unsubs
                        Trace.TraceError("Unrecognized queued item. Discarding it.");
                        await _outgoingRequestList.RemoveAsync(queuedRequest, connectionLostCancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Trace.TraceInformation("Publish message task cancelled.");
            }
            catch (Exception exception)
            {
                Trace.TraceError("Error while publishing queued application messages. {0}", exception);
            }
            finally
            {
                Trace.TraceInformation("Stopped publishing messages.");
            }
        }

        private async Task ExecuteSinglePublishAsync(QueuedPublishRequest queuedPublish, CancellationToken cancellationToken)
        {
            try
            {
                MqttClientPublishResult publishResult = await base.PublishAsync(queuedPublish.Request, cancellationToken);

                await _outgoingRequestList.RemoveAsync(queuedPublish, CancellationToken.None);
                if (!queuedPublish.ResultTaskCompletionSource.TrySetResult(publishResult))
                {
                    Trace.TraceError("Failed to set task completion source for publish request");
                }
            }
            catch (OperationCanceledException)
            {
                if (queuedPublish.CancellationToken.IsCancellationRequested)
                {
                    // User cancelled this request
                    await _outgoingRequestList.RemoveAsync(queuedPublish, CancellationToken.None);
                    queuedPublish.ResultTaskCompletionSource.TrySetCanceled(CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                if (IsFatal(e, false, queuedPublish.CancellationToken.IsCancellationRequested))
                {
                    await _outgoingRequestList.RemoveAsync(queuedPublish, CancellationToken.None);
                    if (!queuedPublish.ResultTaskCompletionSource.TrySetException(e))
                    {
                        Trace.TraceError("Failed to set task completion source for publish request");
                    }
                }
            }
        }

        private async Task ExecuteSingleSubscribeAsync(QueuedSubscribeRequest queuedSubscribe, CancellationToken cancellationToken)
        {
            try
            {
                MqttClientSubscribeResult subscribeResult = await base.SubscribeAsync(queuedSubscribe.Request, cancellationToken);

                await _outgoingRequestList.RemoveAsync(queuedSubscribe, CancellationToken.None);
                if (!queuedSubscribe.ResultTaskCompletionSource.TrySetResult(subscribeResult))
                {
                    Trace.TraceError("Failed to set task completion source for subscribe request");
                }
            }
            catch (OperationCanceledException)
            {
                if (queuedSubscribe.CancellationToken.IsCancellationRequested)
                {
                    // User cancelled this request
                    await _outgoingRequestList.RemoveAsync(queuedSubscribe, CancellationToken.None);
                    queuedSubscribe.ResultTaskCompletionSource.TrySetCanceled(CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                if (IsFatal(e, false, queuedSubscribe.CancellationToken.IsCancellationRequested))
                {
                    await _outgoingRequestList.RemoveAsync(queuedSubscribe, CancellationToken.None);
                    if (!queuedSubscribe.ResultTaskCompletionSource.TrySetException(e))
                    {
                        Trace.TraceError("Failed to set task completion source for subscribe request");
                    }
                }
            }
        }

        private async Task ExecuteSingleUnsubscribeAsync(QueuedUnsubscribeRequest queuedUnsubscribe, CancellationToken cancellationToken)
        {
            try
            {
                MqttClientUnsubscribeResult unsubscribeResult = await base.UnsubscribeAsync(queuedUnsubscribe.Request, cancellationToken);
                await _outgoingRequestList.RemoveAsync(queuedUnsubscribe, CancellationToken.None);
                if (!queuedUnsubscribe.ResultTaskCompletionSource.TrySetResult(unsubscribeResult))
                {
                    Trace.TraceError("Failed to set task completion source for unsubscribe request");
                }
            }
            catch (OperationCanceledException)
            {
                if (queuedUnsubscribe.CancellationToken.IsCancellationRequested)
                {
                    // User cancelled this request
                    await _outgoingRequestList.RemoveAsync(queuedUnsubscribe, CancellationToken.None);
                    queuedUnsubscribe.ResultTaskCompletionSource.TrySetCanceled(CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                if (IsFatal(e, false, queuedUnsubscribe.CancellationToken.IsCancellationRequested))
                {
                    await _outgoingRequestList.RemoveAsync(queuedUnsubscribe, CancellationToken.None);
                    if (!queuedUnsubscribe.ResultTaskCompletionSource.TrySetException(e))
                    {
                        Trace.TraceError("Failed to set task completion source for unsubscribe request");
                    }
                }
            }
        }

        private async Task FinalizeSessionAsync(Exception queuedItemException, MqttClientDisconnectedEventArgs disconnectedEventArgs, CancellationToken cancellationToken)
        {
            if (_isDesiredConnected)
            {
                _isDesiredConnected = false;

                SessionLostAsync?.Invoke(disconnectedEventArgs);
                await _outgoingRequestList.CancelAllRequestsAsync(queuedItemException, cancellationToken);
            }
        }

        // These reason codes are fatal if the broker sends a DISCONNECT packet with this reason.
        private static bool IsFatal(MqttClientDisconnectReason code)
        {
            switch (code)
            {
                case MqttClientDisconnectReason.MalformedPacket:
                case MqttClientDisconnectReason.ProtocolError:
                case MqttClientDisconnectReason.NotAuthorized:
                case MqttClientDisconnectReason.SessionTakenOver:
                case MqttClientDisconnectReason.TopicFilterInvalid:
                case MqttClientDisconnectReason.TopicNameInvalid:
                case MqttClientDisconnectReason.TopicAliasInvalid:
                case MqttClientDisconnectReason.PacketTooLarge:
                case MqttClientDisconnectReason.PayloadFormatInvalid:
                case MqttClientDisconnectReason.RetainNotSupported:
                case MqttClientDisconnectReason.QosNotSupported:
                case MqttClientDisconnectReason.ServerMoved:
                case MqttClientDisconnectReason.SharedSubscriptionsNotSupported:
                case MqttClientDisconnectReason.SubscriptionIdentifiersNotSupported:
                case MqttClientDisconnectReason.WildcardSubscriptionsNotSupported:
                    return true;
            }

            return false;
        }

        private static bool IsFatal(Exception e, bool isReconnecting, bool userCancellationRequested = false)
        {
            if (e is MqttConnectingFailedException)
            {
                MqttClientConnectResultCode code = ((MqttConnectingFailedException)e).ResultCode;

                switch (code)
                {
                    case MqttClientConnectResultCode.MalformedPacket:
                    case MqttClientConnectResultCode.ProtocolError:
                    case MqttClientConnectResultCode.UnsupportedProtocolVersion:
                    case MqttClientConnectResultCode.ClientIdentifierNotValid:
                    case MqttClientConnectResultCode.BadUserNameOrPassword:
                    case MqttClientConnectResultCode.Banned:
                    case MqttClientConnectResultCode.BadAuthenticationMethod:
                    case MqttClientConnectResultCode.TopicNameInvalid:
                    case MqttClientConnectResultCode.PacketTooLarge:
                    case MqttClientConnectResultCode.PayloadFormatInvalid:
                    case MqttClientConnectResultCode.RetainNotSupported:
                    case MqttClientConnectResultCode.QoSNotSupported:
                    case MqttClientConnectResultCode.ServerMoved:
                    case MqttClientConnectResultCode.ImplementationSpecificError:
                    case MqttClientConnectResultCode.UseAnotherServer:
                    case MqttClientConnectResultCode.NotAuthorized:
                        return true;
                }
            }

            if (e is SocketException)
            {
                //TODO there is room for a lot more nuance here. Some socket exceptions are more retryable than others so it may
                // be inappropriate to label them all as fatal.
                return true;
            }

            if (e is MQTTnet.Exceptions.MqttProtocolViolationException)
            {
                return true;
            }

            if (e is ArgumentException
                || e is ArgumentNullException
                || e is NotSupportedException)
            {
                return true;
            }

            // MQTTnet may throw an OperationCanceledException/TaskCanceledException even if
            // neither the user nor the session client provides a cancellation token. Because
            // of that, this exception is only fatal if the cancellation token this layer
            // is aware of actually requested cancellation. Other cases signify that MQTTnet
            // gave up on the operation, but the user still wants to retry.
            if ((e is OperationCanceledException || e is TaskCanceledException)
                && userCancellationRequested)
            {
                return true;
            }

            return false;
        }
    }
}