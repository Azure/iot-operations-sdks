// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"math"
	"sync"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/eclipse/paho.golang/paho"
)

// RegisterConnectEventHandler registers a handler to a list of handlers
// that are called synchronously in registration order whenever the
// SessionClient successfully establishes an MQTT connection. Note that since
// the handler gets called synchronously, handlers should not block for an
// extended period of time to avoid blocking the SessionClient.
func (c *SessionClient) RegisterConnectEventHandler(
	handler ConnectEventHandler,
) (unregisterHandler func()) {
	return c.connectEventHandlers.AppendEntry(handler)
}

// RegisterDisconnectEventHandler registers a handler to a list of
// handlers that are called synchronously in registration order whenever the
// SessionClient detects a disconnection from the MQTT server. Note that since
// the handler gets called synchronously, handlers should not block for an
// extended period of time to avoid blocking the SessionClient.
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

// Start starts the SessionClient, spawning any necessary background goroutines.
// In order to terminate the SessionClient and clean up any running goroutines,
// Stop() must be called after calling Start().
func (c *SessionClient) Start() error {
	if !c.sessionStarted.CompareAndSwap(false, true) {
		return &ClientStateError{State: Started}
	}

	ctx, cancel := context.WithCancelCause(context.Background())

	// https://pkg.go.dev/context#example-AfterFunc-Merge
	c.shutdown = func(c context.Context) (context.Context, context.CancelFunc) {
		c, cause := context.WithCancelCause(c)
		stop := context.AfterFunc(ctx, func() {
			cause(context.Cause(ctx))
		})
		return c, func() {
			stop()
			cause(context.Canceled)
		}
	}
	c.stop = func() { cancel(&ClientStateError{State: ShutDown}) }

	go func() {
		defer c.stop()
		if err := c.manageConnection(ctx); err != nil {
			for handler := range c.fatalErrorHandlers.All() {
				go handler(err)
			}
		}
	}()

	go c.manageOutgoingPublishes(ctx)

	return nil
}

// Stop stops the SessionClient, terminating any pending operations and cleaning
// up background goroutines.
func (c *SessionClient) Stop() error {
	if !c.sessionStarted.Load() {
		return &ClientStateError{State: NotStarted}
	}
	c.stop()
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
		c.conn.Connect(client)

		connectEvent := ConnectEvent{ReasonCode: reasonCode}
		for handler := range c.connectEventHandlers.All() {
			handler(&connectEvent)
		}
	}

	signalDisconnection := func(reasonCode *byte) {
		c.conn.Disconnect()

		disconnectEvent := DisconnectEvent{ReasonCode: reasonCode}
		for handler := range c.disconnectEventHandlers.All() {
			handler(&disconnectEvent)
		}
	}

	// On cleanup, send a DISCONNECT packet if possible and signal a
	// disconnection to other goroutines if needed.
	defer func() {
		pahoClient := c.conn.Current().Client
		if pahoClient == nil {
			return
		}
		immediateSessionExpiry := uint32(0)
		disconn := &paho.Disconnect{
			ReasonCode: disconnectNormalDisconnection,
			Properties: &paho.DisconnectProperties{
				SessionExpiryInterval: &immediateSessionExpiry,
			},
		}
		c.log.Packet(ctx, "disconnect", disconn)
		_ = pahoClient.Disconnect(disconn)
		signalDisconnection(nil)
	}()

	for {
		var pahoClient PahoClient
		var disconnected <-chan *pahoClientDisconnectedEvent
		var connectReasonCode *byte
		err := c.connRetry.Start(ctx, "connect",
			func(ctx context.Context) (bool, error) {
				var err error

				connCtx := ctx
				if c.config.connectionTimeout != 0 {
					// timeout for this single connection attempt
					var cancel func()
					connCtx, cancel = context.WithTimeout(ctx, c.config.connectionTimeout)
					defer cancel()
				}

				pahoClient, connectReasonCode, disconnected, err = c.buildPahoClient(
					connCtx,
					c.conn.Current().Count,
				)
				return !isFatalError(err), err
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

	// buffer the channel by 1 to avoid hanging a goroutine in the case where
	// paho has an error AND the server sends a DISCONNECT packet.
	disconnected := make(chan *pahoClientDisconnectedEvent, 1)
	var clientErrOnce, serverDisconnectOnce sync.Once

	pahoClient, err := c.pahoConstructor(ctx, &paho.ClientConfig{
		ClientID:    c.config.clientID,
		Session:     c.session,
		AuthHandler: &pahoAuther{c: c},
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
	if err != nil {
		return nil, nil, nil, err
	}

	conn, err := buildConnectPacket(
		ctx,
		c.config,
		isInitialConn,
	)

	if err != nil {

	}

	// TODO: timeout if CONNACK doesn't come back in a reasonable amount of time
	c.log.Packet(ctx, "connect", conn)
	connack, err := pahoClient.Connect(ctx, conn)
	c.log.Packet(ctx, "connack", connack)

	if connack == nil {
		// This assumes that all errors returned by Paho's connect method
		// without a CONNACK are non-fatal.
		return nil, nil, nil, err
	}

	if connack.ReasonCode >= 80 {
		var connackError error = &ConnackError{ReasonCode: connack.ReasonCode}
		if isFatalConnackReasonCode(connack.ReasonCode) {
			connackError = fatalError{connackError}
		}
		return nil, &connack.ReasonCode, nil, connackError
	}

	// NOTE: there is no way for the user to know if the session was present if
	// this is the first connection and firstConnectionCleanStart is set to
	// false
	if !isInitialConn && !connack.SessionPresent {
		immediateSessionExpiry := uint32(0)
		_ = pahoClient.Disconnect(&paho.Disconnect{
			ReasonCode: disconnectNormalDisconnection,
			Properties: &paho.DisconnectProperties{
				SessionExpiryInterval: &immediateSessionExpiry,
			},
		})
		return nil, &connack.ReasonCode, nil, fatalError{&SessionLostError{}}
	}

	return pahoClient, &connack.ReasonCode, disconnected, nil
}

func (c *SessionClient) defaultPahoConstructor(
	ctx context.Context,
	cfg *paho.ClientConfig,
) (PahoClient, error) {
	conn, err := c.config.connectionProvider(ctx)
	if err != nil {
		return nil, err
	}

	cfg.Conn = conn
	return paho.NewClient(*cfg), nil
}

func buildConnectPacket(
	ctx context.Context,
	config *connectionConfig,
	isInitialConn bool,
) (*paho.Connect, error) {
	sessionExpiryInterval := config.sessionExpiryInterval
	properties := paho.ConnectProperties{
		SessionExpiryInterval: &sessionExpiryInterval,
		ReceiveMaximum:        &config.receiveMaximum,
		// https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901053
		// We need user properties by default.
		RequestProblemInfo: true,
		User: internal.MapToUserProperties(
			config.userProperties,
		),
	}

	packet := &paho.Connect{
		ClientID:   config.clientID,
		CleanStart: isInitialConn && config.firstConnectionCleanStart,
		KeepAlive:  config.keepAlive,
		Properties: &properties,
	}

	userName, userNameFlag, err := config.userNameProvider(ctx)
	if err != nil {
		return nil, &InvalidArgumentError{
			wrappedError: err,
			message:      "error getting user name from UserNameProvider",
		}
	}
	if userNameFlag {
		packet.UsernameFlag = true
		packet.Username = userName
	}

	password, passwordFlag, err := config.passwordProvider(ctx)
	if err != nil {
		return nil, &InvalidArgumentError{
			wrappedError: err,
			message:      "error getting password from PasswordProvider",
		}
	}
	if passwordFlag {
		packet.PasswordFlag = true
		packet.Password = password
	}

	return packet, nil
}
