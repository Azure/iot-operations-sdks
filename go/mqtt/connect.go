// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"math"
	"sync"

	"github.com/Azure/iot-operations-sdks/go/mqtt/retrypolicy"
	"github.com/eclipse/paho.golang/paho"
)

type (
	ConnackPacket struct {
		ReasonCode byte
		// NOTE: more fields may be added later
		// NOTE: this may be moved to the common module once we create it
	}

	ConnectEvent struct {
		// Values from the CONNACK packet received from the MQTT server
		ConnackPacket *ConnackPacket
	}

	ConnectNotificationHandler = func(*ConnectEvent)

	DisconnectPacket struct {
		ReasonCode byte
		// NOTE: more fields may be added later
		// NOTE: this may be moved to the common module once we create it
	}

	DisconnectEvent struct {
		// Values from the DISCONNECT packet received from the MQTT server. May
		// be nil if the disconnection ocurred without receiving a DISCONNECT
		// packet from the server.
		DisconnectPacket *DisconnectPacket
	}

	DisconnectNotificationHandler = func(*DisconnectEvent)
)

// RegisterConnectNotificationHandler registers a handler to a list of handlers
// that are called synchronously in registration order whenever the
// SessionClient successfully establishes an MQTT connection. Note that since
// the handler gets called synchronously, handlers should not block for an
// extended period of time to avoid blocking the SessionClient.
func (c *SessionClient) RegisterConnectNotificationHandler(
	handler ConnectNotificationHandler,
) (unregisterHandler func()) {
	return c.connectNotificationHandlers.AppendEntry(handler)
}

// RegisterDisconnectNotificationHandler registers a handler to a list of
// handlers that are called synchronously in registration order whenever the
// SessionClient detects a disconnection from the MQTT server. Note that since
// the handler gets called synchronously, handlers should not block for an
// extended period of time to avoid blocking the SessionClient.
func (c *SessionClient) RegisterDisconnectNotificationHandler(
	handler DisconnectNotificationHandler,
) (unregisterHandler func()) {
	return c.disconnectNotificationHandlers.AppendEntry(handler)
}

// RegisterFatalErrorHandler registers a handler that is called in a goroutine
// if the SessionClient terminates due to a fatal error.
func (c *SessionClient) RegisterFatalErrorHandler(
	handler func(error),
) (unregisterHandler func()) {
	return c.fatalErrorHandlers.AppendEntry(handler)
}

// Start starts the SessionClient, spawning any necessary background goroutines.
// In order to terminate the SessionClient and clean up any running goroutines,
// Stop() must be called after calling Start().
func (c *SessionClient) Start() error {
	if !c.sessionStarted.CompareAndSwap(false, true) {
		return &ClientStateError{State: Started}
	}

	clientShutdownCtx, clientShutdownFunc := context.WithCancel(
		context.Background(),
	)

	go func() {
		defer clientShutdownFunc()
		defer close(c.shutdown)
		select {
		case <-clientShutdownCtx.Done():
		case <-c.userStop:
		}
	}()

	go func() {
		defer clientShutdownFunc()
		err := c.manageConnection(clientShutdownCtx)
		if err != nil {
			for handler := range c.fatalErrorHandlers.All() {
				go handler(err)
			}
		}
	}()

	go c.manageOutgoingPublishes(clientShutdownCtx)

	return nil
}

// Stop stops the SessionClient, terminating any pending operations and cleaning
// up background goroutines.
func (c *SessionClient) Stop() error {
	if !c.sessionStarted.Load() {
		return &ClientStateError{State: NotStarted}
	}
	c.closeUserStopOnce.Do(func() { close(c.userStop) })
	return nil
}

type pahoClientDisconnectedEvent struct {
	err              error
	disconnectPacket *paho.Disconnect
}

// Attempts an initial connection and then listens for disconnections to attempt
// reconnections. Blocks until the ctx is cancelled or the connection can no
// longer be maintained (due to a fatal error or retry policy exhaustion).
func (c *SessionClient) manageConnection(ctx context.Context) error {
	signalConnection := func(client PahoClient, reasonCode byte) {
		func() {
			c.pahoClientMu.Lock()
			defer c.pahoClientMu.Unlock()

			c.pahoClient = client
			close(c.connUp)
			c.connDown = make(chan struct{})
			c.connCount++
		}()

		connectEvent := ConnectEvent{
			ConnackPacket: &ConnackPacket{
				ReasonCode: reasonCode,
			},
		}
		for handler := range c.connectNotificationHandlers.All() {
			handler(&connectEvent)
		}
	}

	signalDisconnection := func(reasonCode *byte) {
		func() {
			c.pahoClientMu.Lock()
			defer c.pahoClientMu.Unlock()

			c.pahoClient = nil
			c.connUp = make(chan struct{})
			close(c.connDown)
		}()

		disconnectEvent := DisconnectEvent{}
		if reasonCode != nil {
			disconnectEvent.DisconnectPacket = &DisconnectPacket{
				ReasonCode: *reasonCode,
			}
		}

		for handler := range c.disconnectNotificationHandlers.All() {
			handler(&disconnectEvent)
		}
	}

	// On cleanup, send a DISCONNECT packet if possible and signal a
	// disconnection to other goroutines if needed.
	defer func() {
		// NOTE: accessing c.pahoClient is thread safe here because
		// manageConnection is no longer writing to c.pahoClient if we get to
		// this deferred function.
		if c.pahoClient == nil {
			return
		}
		immediateSessionExpiry := uint32(0)
		_ = c.pahoClient.Disconnect(&paho.Disconnect{
			ReasonCode: disconnectNormalDisconnection,
			Properties: &paho.DisconnectProperties{
				SessionExpiryInterval: &immediateSessionExpiry,
			},
		})
		signalDisconnection(nil)
	}()

	for {
		var pahoClient PahoClient
		var disconnected <-chan *pahoClientDisconnectedEvent
		var connectReasonCode *byte
		err := c.connRetry.Start(
			ctx,
			c.info,
			retrypolicy.Task{
				Name: "connect",
				Exec: func(ctx context.Context) error {
					var err error
					pahoClient, connectReasonCode, disconnected, err = c.buildPahoClient(
						ctx,
						c.connCount,
					)
					return err
				},
				Cond: isRetryableError,
			},
		)
		if err != nil {
			return &RetryFailureError{lastError: err}
		}
		signalConnection(pahoClient, *connectReasonCode)

		var disconnectEvent *pahoClientDisconnectedEvent
		select {
		case disconnectEvent = <-disconnected:
			// Current paho instance got disconnected
		case <-ctx.Done():
			// SessionClient is shutting down
			return nil
		}

		var disconnectReasonCode *byte
		if disconnectEvent.disconnectPacket != nil {
			disconnectReasonCode = &disconnectEvent.disconnectPacket.ReasonCode
		}
		signalDisconnection(disconnectReasonCode)
		if disconnectReasonCode != nil &&
			isFatalDisconnectReasonCode(*disconnectReasonCode) {
			return &FatalDisconnectError{
				ReasonCode: *disconnectReasonCode,
			}
		}

		// if we get here, a reconnection will be attempted
	}
}

// buildPahoClient creates an instance of a Paho client and attempts to connect
// it to the MQTT server. If the client is successfully connected, the client
// instance is returned along with a channel to be notified when the connection
// on that client instance goes down.
func (c *SessionClient) buildPahoClient(
	ctx context.Context,
	connCount uint64,
) (PahoClient, *byte, <-chan *pahoClientDisconnectedEvent, error) {
	isInitialConn := connCount == 0

	// Refresh TLS config for new connection.
	if err := c.connSettings.validateTLS(); err != nil {
		// TODO: this currently returns immediately if refreshing TLS config
		// fails. Do we want to instead attempt to connect with the stale TLS
		// config?
		return nil, nil, nil, err
	}

	conn, err := buildNetConn(
		ctx,
		c.connSettings.serverURL,
		c.connSettings.tlsConfig,
	)
	if err != nil {
		// buildNetConn will wrap the error in retryableErr if it's retryable
		return nil, nil, nil, err
	}

	// buffer the channel by 1 to avoid hanging a goroutine in the case where
	// paho has an error AND the server sends a DISCONNECT packet.
	disconnected := make(chan *pahoClientDisconnectedEvent, 1)
	var clientErrOnce, serverDisconnectOnce sync.Once

	pahoClient := paho.NewClient(paho.ClientConfig{
		ClientID: c.connSettings.clientID,
		Conn:     conn,
		Session:  c.session,
		OnPublishReceived: []func(paho.PublishReceived) (bool, error){
			// add 1 to the conn count for this because this listener is
			// effective AFTER the connection succeeds
			c.makeOnPublishReceived(connCount + 1),
		},
		// Set Paho's packet timeout to the maximum possible value to
		// effectively disable it. We can still control any timeouts through the
		// contexts we pass into Paho.
		PacketTimeout: math.MaxInt64,
		OnServerDisconnect: func(d *paho.Disconnect) {
			serverDisconnectOnce.Do(func() {
				disconnected <- &pahoClientDisconnectedEvent{disconnectPacket: d}
			})
		},
		OnClientError: func(err error) {
			clientErrOnce.Do(func() {
				disconnected <- &pahoClientDisconnectedEvent{err: err}
			})
		},
		// Disable automatic acking in Paho. The session client will manage acks
		// instead.
		EnableManualAcknowledgment: true,
	})

	cp := buildConnectPacket(
		c.connSettings.clientID,
		c.connSettings,
		isInitialConn,
	)

	c.logConnect(cp)

	// TODO: timeout if CONNACK doesn't come back in a reasonable amount of time
	connack, err := pahoClient.Connect(ctx, cp)

	if connack == nil {
		// This assumes that all errors returned by Paho's connect method
		// without a CONNACK are retryable.
		return nil, nil, nil, retryableErr{err}
	}

	if connack.ReasonCode >= 80 {
		var connackError error = &ConnackError{ReasonCode: connack.ReasonCode}
		if !isFatalConnackReasonCode(connack.ReasonCode) {
			connackError = retryableErr{connackError}
		}
		return nil, &connack.ReasonCode, nil, connackError
	}
	if !isInitialConn && !connack.SessionPresent {
		return nil, &connack.ReasonCode, nil, &SessionLostError{}
	}

	return pahoClient, &connack.ReasonCode, disconnected, nil
}
