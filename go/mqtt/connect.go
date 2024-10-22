// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	e "errors"
	"fmt"
	"io"
	"sync/atomic"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/Azure/iot-operations-sdks/go/mqtt/retry"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/eclipse/paho.golang/paho"
	"github.com/eclipse/paho.golang/paho/session/state"
)

// RegisterConnectEventHandler registers a handler to a list of handlers that
// are called synchronously in registration order whenever the SessionClient
// successfully establishes an MQTT connection. Note that since the handler
// gets called synchronously, handlers should not block for an extended period
// of time to avoid blocking the SessionClient.
func (c *SessionClient) RegisterConnectEventHandler(
	handler ConnectEventHandler,
) (unregisterHandler func()) {
	return c.connectEventHandlers.AppendEntry(handler)
}

// RegisterDisconnectEventHandler registers a handler to a list of handlers that
// are called synchronously in registration order whenever the SessionClient
// detects a disconnection from the MQTT server. Note that since the handler
// gets called synchronously, handlers should not block for an extended period
// of time to avoid blocking the SessionClient.
func (c *SessionClient) RegisterDisconnectEventHandler(
	handler DisconnectEventHandler,
) (unregisterHandler func()) {
	return c.disconnectEventHandlers.AppendEntry(handler)
}

// RegisterFatalErrorHandler registers a handler that is called in a goroutine
// if the SessionClient terminates due to a fatal error.
func (c *SessionClient) RegisterFatalErrorHandler(
	handler func(error),
) (unregisterHandler func()) {
	return c.fatalErrorHandlers.AppendEntry(handler)
}

// Connect establishes a connection for the session client.
func (c *SessionClient) Connect(ctx context.Context) error {
	// The initial connection will block until it either succeeds or fails.
	if connErr := c.initialConnect(ctx); connErr != nil {
		return connErr
	}

	// Start automatic reauthentication in the background
	// if user set AuthDataProvider to refresh auth data.
	if c.connSettings.authOptions.AuthDataProvider != nil {
		go c.autoAuth()
	}

	// Monitor the connection in the background
	// after the initial connection succeeds.
	go c.maintain()

	return nil
}

// Disconnect closes the connection gracefully
// by sending the disconnect packet to server
// and should terminate any active goroutines before returning.
func (c *SessionClient) Disconnect() error {
	ctx := context.TODO()

	if err := c.ensurePahoClient(ctx); err != nil {
		return err
	}

	if !c.isConnected.Load() {
		return &errors.Error{
			Kind:    errors.StateInvalid,
			Message: "Cannot disconnect since the client is not connected",
		}
	}

	c.log.Info(ctx, "start disconnection")

	// Exit all background goroutines.
	close(c.connStopC.C)

	// Sending disconnect packet to server.
	disconnErr := c.attemptDisconnect()
	if disconnErr != nil {
		c.log.Error(ctx, fmt.Errorf(
			"an error ocurred during disconnection: %s",
			disconnErr.Error(),
		))
		return disconnErr
	}

	c.log.Info(ctx, "disconnected")

	return nil
}

// Maintain automatically reconnects the client when the connection drops.
func (c *SessionClient) maintain() {
	ctx, cancel := context.WithCancel(context.Background())
	var err error

	// defer an anonymous function so err would be captured into shutdown()
	defer func() {
		c.shutdown()
		cancel()
	}()

	for {
		select {
		// Handle errors from the ongoing client.
		case err = <-c.clientErrC.C:
			if !c.recover(ctx, err) {
				return
			}
		// Handle server disconnection errors.
		case err = <-c.disconnErrC.C:
			if !c.recover(ctx, err) {
				return
			}
		case <-c.connStopC.C:
			return
		}
	}
}

// recover tries to recover connection from
// client error and server disconnection error.
func (c *SessionClient) recover(
	ctx context.Context,
	err error,
) bool {
	// nil error could be sent out from channel as well.
	if err != nil {
		// Note: An EOF error indicates that the server is disconnecting
		// the client by closing the network connection,
		// possibly due to credential expiry or other reasons.
		// In this scenario, the server does not send any packet to the client,
		// so the exact reason for disconnection should be unknown.
		// Normally, for MQ, the EOF error would persist for 1 minute,
		// after which the server would send a 0x86 fatal reason code,
		// then client would stop permanently.
		if e.Is(err, io.EOF) {
			err = fmt.Errorf(
				"server closed connection: %w; "+
					"expired client credentials or broker offline",
				err,
			)
		}
		c.log.Error(ctx, fmt.Errorf(
			"an error occurs during connection: %s",
			err.Error()))
	}

	// Reconnect.
	c.log.Info(ctx, "start reconnection")
	if connErr := c.reconnect(ctx); connErr != nil {
		c.log.Error(
			ctx,
			fmt.Errorf(
				"non-retryable fatal error returns; disconnecting the client",
			),
		)
		for handler := range c.fatalErrorHandlers.All() {
			go handler(err)
		}
		return false
	}

	// Blocking all subscribe/unsubscribe/updateSubscription/publish
	// operations until we finish sending all requests in the queue
	// to ensure request ordering.
	c.processBuffer(ctx)

	return true
}

// initialConnect starts the initial connection with a new session.
func (c *SessionClient) initialConnect(
	ctx context.Context,
) error {
	// Start a new session.
	c.session = state.NewInMemory()

	r := c.connRetry
	// By default we retry only a few times for the initial connection attempt.
	if c.connRetry == nil {
		r = &retry.ExponentialBackoff{
			MaxAttempts: uint64(maxInitialConnectRetries),
			Timeout:     c.connSettings.connectionTimeout,
		}
	}

	err := r.Start(ctx, "connect", c.attemptConnect)
	if err == nil {
		c.log.Info(ctx, "initial connection established")
		atomic.StoreInt64(&c.connCount, 1)
		c.setConnected()
	}

	return err
}

// reconnect attempts to re-establish the connection with the existing session
// if the initial successful connection is lost.
func (c *SessionClient) reconnect(ctx context.Context) error {
	r := c.connRetry
	// We keep retrying forever until fatal errors
	// to guarantee the connection by default.
	if c.connRetry == nil {
		r = &retry.ExponentialBackoff{
			Timeout: c.connSettings.connectionTimeout,
		}
	}

	err := r.Start(ctx, "connect", c.attemptConnect)
	if err == nil {
		c.log.Info(ctx, "reconnected")

		atomic.AddInt64(&c.connCount, 1)
		c.setConnected()
	}

	return err
}

// attemptConnect represents a single connection attempt
// for either initialConnect() or reconnect().
func (c *SessionClient) attemptConnect(ctx context.Context) (bool, error) {
	if err := c.buildPahoClient(ctx); err != nil {
		return isRetryableError(err), err
	}

	c.clientErrC = *internal.NewBufferChan[error](1)
	c.disconnErrC = *internal.NewBufferChan[error](1)

	// Renew auth token in case the disconnection was due to token expiration.
	if c.connSettings.authOptions.AuthDataProvider != nil {
		WithAuthData(c.connSettings.authOptions.AuthDataProvider(ctx))(c)
	}

	cp := buildConnectPacket(
		c.connSettings.clientID,
		c.connSettings,
		atomic.LoadInt64(&c.connCount) == 0,
	)

	// TODO: Handle connack packet in the specific handler/callback.
	// Note that connack and actual error could possibly be returned together.
	c.log.Packet(ctx, "connect", cp)
	connack, err := pahoConn(ctx, c.pahoClient, cp)
	if err != nil {
		return connack == nil ||
			isRetryableConnack(reasonCode(connack.ReasonCode)), err
	}

	for handler := range c.connectEventHandlers.All() {
		go handler(&ConnectEvent{ReasonCode: connack.ReasonCode})
	}

	return false, nil
}

func (c *SessionClient) attemptDisconnect() error {
	ctx := context.TODO()
	dp := buildDisconnectPacket(
		disconnectNormalDisconnection,
		"connection context cancellation",
	)
	c.log.Packet(ctx, "disconnect", dp)
	return pahoDisconn(c.pahoClient, dp)
}

// bufferPacket adds a packet to the queue and waits for future reconnection.
func (c *SessionClient) bufferPacket(
	ctx context.Context,
	pq *queuedPacket,
) error {
	c.log.Info(ctx, fmt.Sprintf(
		"connection lost; buffer packet: %#v",
		pq.packet,
	))

	if c.pendingPackets.IsFull() {
		return &errors.Error{
			Kind: errors.ExecutionException,
			Message: fmt.Sprintf(
				"%s cannot be enqueued as the queue is full",
				pq.packetType(),
			),
		}
	}

	pq.errC = make(chan error, 1)
	c.pendingPackets.Enqueue(*pq)

	// Blocking until we get expected response from reconnection.
	c.log.Info(ctx, "waiting for packet response after reconnection")
	select {
	case err, ok := <-pq.errC:
		if ok {
			return err
		}
		return nil
	case <-ctx.Done():
		return &errors.Error{
			Kind: errors.StateInvalid,
			Message: fmt.Sprintf(
				"Cannot send %s because context was canceled",
				pq.packetType(),
			),
			NestedError: ctx.Err(),
		}
	}
}

// processBuffer starts processing pending packets in the queue
// after a successful reconnection.
func (c *SessionClient) processBuffer(ctx context.Context) {
	c.log.Info(ctx, "start processing pending packets after reconnection")
	if c.pendingPackets.IsEmpty() {
		c.log.Info(ctx, "no pending packets in the queue")
	}

	for !c.pendingPackets.IsEmpty() {
		c.log.Info(ctx,
			fmt.Sprintf("%d packet(s) in the queue", c.pendingPackets.Size()),
		)
		qp := c.pendingPackets.Dequeue()
		if qp != nil {
			switch p := qp.packet.(type) {
			case *paho.Publish:
				c.log.Packet(ctx, "publish", p)
				qp.handleError(pahoPub(ctx, c.pahoClient, p))
			case *paho.Subscribe:
				c.log.Packet(ctx, "subscribe", p)
				qp.handleError(pahoSub(ctx, c.pahoClient, p))
			case *paho.Unsubscribe:
				c.log.Packet(ctx, "unsubscribe", p)
				qp.handleError(pahoUnsub(ctx, c.pahoClient, p))
			default:
				c.log.Error(ctx,
					fmt.Errorf(
						"cannot process unknown packet in queue: %v",
						qp,
					),
				)
				continue
			}
		}
	}

	// Unblock other operations.
	c.log.Info(
		ctx,
		"pending packets processing completes; resume other operations",
	)
	c.packetQueueCond.Broadcast()
}

// buildPahoClient builds the Paho client from either testing provided
// Paho config or internal constructed config.
func (c *SessionClient) buildPahoClient(ctx context.Context) error {
	var config *paho.ClientConfig
	if c.pahoClientConfig != nil {
		// For testing infrastructure.
		config = c.pahoClientConfig

		if config.ClientID != "" {
			c.connSettings.clientID = config.ClientID
		}

		if config.Session != nil {
			c.session = config.Session
		}

		if config.AuthHandler != nil {
			c.connSettings.authOptions.AuthHandler = config.AuthHandler
		}
	} else {
		// Refresh TLS config for new connection.
		if err := c.connSettings.validateTLS(); err != nil {
			return err
		}

		conn, err := buildNetConn(
			ctx,
			c.connSettings.serverURL,
			c.connSettings.tlsConfig,
		)
		if err != nil {
			return err
		}
		config = &paho.ClientConfig{
			Conn:        conn,
			ClientID:    c.connSettings.clientID,
			Session:     c.session,
			AuthHandler: c.connSettings.authOptions.AuthHandler,
		}
	}

	config.EnableManualAcknowledgment = true
	config.OnClientError = c.onClientError
	config.OnServerDisconnect = c.onServerDisconnect
	config.OnPublishReceived = []func(paho.PublishReceived) (bool, error){
		c.onPublishReceived,
	}

	c.pahoClientMu.Lock()
	defer c.pahoClientMu.Unlock()
	c.pahoClient = c.pahoClientFactory(config)

	return nil
}

// prepare validates the connection status and packet queue
// before sending subscribe/unsubscribe/publish packets.
func (c *SessionClient) prepare(ctx context.Context) error {
	if err := c.ensurePahoClient(ctx); err != nil {
		return err
	}

	// Initial connection failed after retries, so no responses are expected
	// due to the lack of recovery.
	if !c.isConnected.Load() && atomic.LoadInt64(&c.connCount) < 1 {
		err := &errors.Error{
			Kind:        errors.StateInvalid,
			Message:     "no initial connection; request cannot be executed",
			NestedError: ctx.Err(),
		}
		c.log.Error(ctx, err)
		return err
	}

	// The operation will block if the connection is up and
	// there are pending packets in the queue.
	// If the connection is down, packets will be added to the queue directly.
	// Users can spawn a goroutine to call the function and unblock their codes.
	for !c.pendingPackets.IsEmpty() && c.isConnected.Load() {
		c.log.Info(ctx, "pending packets in the queue; wait for the process")
		c.packetQueueCond.Wait()
	}

	return nil
}

// Note: Shutdown may occur simultaneously while sending a packet
// if the user calls `Disconnect()` and sends a disconnect packet to the server.
// Because c.connStopC is closed before sending the disconnect packet
// to avoid the network closure triggering another error from onClientError,
// which could initiate an unnecessary automatic reconnect.
// That's also why we don't set c.pahoClient to nil here,
// as packet sending requires the Paho client.
// Since the program's termination point is uncertain,
// we can't clean up the Paho client.
// This is acceptable because it will be recreated with each new connection.
func (c *SessionClient) shutdown() {
	ctx := context.TODO()

	c.log.Info(ctx, "client is shutting down")

	c.setDisconnected()
	c.closeClientErrC()
	c.closeDisconnErrC()
}

func (c *SessionClient) onClientError(err error) {
	ctx := context.TODO()

	for handler := range c.fatalErrorHandlers.All() {
		go handler(err)
	}

	if !c.isConnected.Load() {
		return
	}

	c.log.Info(ctx, "an error from onClientError occurs")
	c.setDisconnected()

	if err != nil && !c.clientErrC.Send(err) {
		c.log.Error(ctx,
			fmt.Errorf(
				"failed to send error from onClientError; "+
					"internal channel closed: %s",
				err.Error(),
			),
		)
	}
}

func (c *SessionClient) onServerDisconnect(disconnect *paho.Disconnect) {
	ctx := context.TODO()

	for handler := range c.disconnectEventHandlers.All() {
		go handler(&DisconnectEvent{ReasonCode: &disconnect.ReasonCode})
	}

	if !c.isConnected.Load() {
		return
	}

	c.log.Info(ctx, "server sent a disconnect packet")

	c.setDisconnected()

	var err error
	if disconnect != nil &&
		isRetryableDisconnect(reasonCode(disconnect.ReasonCode)) {
		err = disconnErr.Translate(context.Background(), disconnect, nil)
	}

	if err != nil && !c.disconnErrC.Send(err) {
		c.log.Error(ctx,
			fmt.Errorf(
				"failed to send error from onServerDisconnect; "+
					"internal channel closed: %s",
				err.Error(),
			),
		)
	}
}

func (c *SessionClient) setConnected() {
	c.isConnected.Store(true)
}

func (c *SessionClient) setDisconnected() {
	c.isConnected.Store(false)
}

func (c *SessionClient) closeClientErrC() {
	c.clientErrC.Close()
}

func (c *SessionClient) closeDisconnErrC() {
	c.disconnErrC.Close()
}
