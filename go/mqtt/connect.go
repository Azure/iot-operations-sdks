// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"log/slog"
	"math"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/eclipse/paho.golang/paho"
)

// RegisterConnectEventHandler registers a handler to a list of handlers that
// are called synchronously in registration order whenever the session client
// successfully establishes an MQTT connection. Note that since the handler
// gets called synchronously, handlers should not block for an extended period
// of time to avoid blocking the session client.
func (c *SessionClient) RegisterConnectEventHandler(
	handler ConnectEventHandler,
) (unregisterHandler func()) {
	return c.connectEventHandlers.AppendEntry(handler)
}

// RegisterDisconnectEventHandler registers a handler to a list of handlers that
// are called synchronously in registration order whenever the session client
// detects a disconnection from the MQTT server. Note that since the handler
// gets called synchronously, handlers should not block for an extended period
// of time to avoid blocking the session client.
func (c *SessionClient) RegisterDisconnectEventHandler(
	handler DisconnectEventHandler,
) (unregisterHandler func()) {
	return c.disconnectEventHandlers.AppendEntry(handler)
}

// RegisterFatalErrorHandler registers a handler that is called in a goroutine
// if the session client terminates due to a fatal error.
func (c *SessionClient) RegisterFatalErrorHandler(
	handler func(error),
) (unregisterHandler func()) {
	return c.fatalErrorHandlers.AppendEntry(handler)
}

// Start starts the session client, spawning any necessary background
// goroutines. In order to terminate the session client and clean up any
// running goroutines, Stop() must be called after calling Start().
func (c *SessionClient) Start() error {
	if !c.sessionStarted.CompareAndSwap(false, true) {
		return &ClientStateError{State: Started}
	}

	c.shutdown = internal.NewBackground(&ClientStateError{State: ShutDown})
	ctx, _ := c.shutdown.With(context.Background())

	go func() {
		defer c.shutdown.Close()
		if err := c.manageConnection(ctx); err != nil {
			c.log.Error(ctx, err)
			for handler := range c.fatalErrorHandlers.All() {
				go handler(err)
			}
		}
	}()

	go c.manageOutgoingPublishes(ctx)

	return nil
}

// Stop stops the session client, terminating any pending operations and
// cleaning up background goroutines.
func (c *SessionClient) Stop() error {
	if !c.sessionStarted.Load() {
		return &ClientStateError{State: NotStarted}
	}
	c.shutdown.Close()
	return nil
}

// Attempts an initial connection and then listens for disconnections to attempt
// reconnections. Blocks until the ctx is cancelled or the connection can no
// longer be maintained (due to a fatal error or retry policy exhaustion).
func (c *SessionClient) manageConnection(ctx context.Context) error {
	// On cleanup, send a DISCONNECT packet if possible and signal a
	// disconnection to other goroutines if needed.
	defer func() {
		pahoClient := c.conn.Current().Client
		if pahoClient == nil {
			return
		}
		c.forceDisconnect(ctx, pahoClient)
		c.signalDisconnection(ctx, &DisconnectEvent{})
	}()

	var reconnect bool
	for {
		var connack *paho.Connack
		err := c.connRetry.Start(ctx, "connect",
			func(ctx context.Context) (bool, error) {
				var err error

				connCtx := ctx
				if c.config.connectionTimeout != 0 {
					var cancel func()
					connCtx, cancel = context.WithTimeout(
						ctx,
						c.config.connectionTimeout,
					)
					defer cancel()
				}

				connack, err = c.connect(connCtx, reconnect)

				// Decide to retry depending on whether we consider this error
				// to be fatal. We don't wrap these errors, so we can use a
				// simple type-switch instead of Go error wrapping.
				switch err.(type) {
				case *InvalidArgumentError,
					*SessionLostError,
					*FatalConnackError,
					*FatalDisconnectError:
					return false, err
				default:
					return true, err
				}
			},
		)
		if err != nil {
			return err
		}

		// NOTE: signalConnection and signalDisconnection must only be called
		// together in this loop to ensure ordering between the two.
		c.signalConnection(ctx, &ConnectEvent{ReasonCode: connack.ReasonCode})
		reconnect = true

		select {
		case <-c.conn.Current().Down.Done():
			// Current paho instance got disconnected.
			switch err := c.conn.Current().Error.(type) {
			case *FatalDisconnectError:
				c.signalDisconnection(ctx, &DisconnectEvent{
					ReasonCode: &err.ReasonCode,
				})
				return err

			case *DisconnectError:
				c.signalDisconnection(ctx, &DisconnectEvent{
					ReasonCode: &err.ReasonCode,
				})

			default:
				c.signalDisconnection(ctx, &DisconnectEvent{
					Error: err,
				})
			}

		case <-ctx.Done():
			// Session client is shutting down.
			return nil
		}

		// if we get here, a reconnection will be attempted
	}
}

// Create an instance of a Paho client and attempts to connect it to the MQTT
// server. If the client is successfully connected, return a channel which will
// be notified when the connection on that client instance goes down, and
// whether or not that disconnection is due to a fatal error.
func (c *SessionClient) connect(
	ctx context.Context,
	reconnect bool,
) (*paho.Connack, error) {
	attempt := c.conn.Attempt()

	var auther paho.Auther
	if c.config.authProvider != nil {
		auther = &pahoAuther{c: c}
	}

	pahoClient, err := c.pahoConstructor(ctx, &paho.ClientConfig{
		ClientID: c.config.clientID,
		Session:  c.session,

		// Set Paho's packet timeout to the maximum possible value to
		// effectively disable it. We can still control any timeouts through the
		// contexts we pass into Paho.
		PacketTimeout: math.MaxInt64,

		// Disable automatic acking in Paho. The session client will manage acks
		// instead.
		EnableManualAcknowledgment: true,

		OnPublishReceived: []func(paho.PublishReceived) (bool, error){
			// Add 1 to the conn count for this because this listener is
			// effective AFTER the connection succeeds.
			c.makeOnPublishReceived(ctx, attempt),
		},
		OnServerDisconnect: func(d *paho.Disconnect) {
			if isFatalDisconnectReasonCode(d.ReasonCode) {
				c.conn.Disconnect(attempt, &FatalDisconnectError{d.ReasonCode})
			} else {
				c.conn.Disconnect(attempt, &DisconnectError{d.ReasonCode})
			}
		},

		OnClientError: func(err error) {
			c.conn.Disconnect(attempt, err)
		},

		AuthHandler: auther,
	})
	if err != nil {
		return nil, err
	}

	conn, err := c.buildConnectPacket(ctx, reconnect)
	if err != nil {
		return nil, err
	}

	// NOTE: there is no way for the user to know if the session was present if
	// this is the first connection and firstConnectionCleanStart is set to
	// false
	c.log.Packet(ctx, "connect", conn)
	connack, err := pahoClient.Connect(ctx, conn)
	c.log.Packet(ctx, "connack", connack)

	switch {
	case connack == nil:
		// This assumes that all errors returned by Paho's connect method
		// without a CONNACK are non-fatal.
		return nil, err

	case isFatalConnackReasonCode(connack.ReasonCode):
		return nil, &FatalConnackError{connack.ReasonCode}

	case connack.ReasonCode >= 80:
		return nil, &ConnackError{connack.ReasonCode}

	case reconnect && !connack.SessionPresent:
		c.forceDisconnect(ctx, pahoClient)
		return nil, &SessionLostError{}

	default:
		if err := c.conn.Connect(pahoClient); err != nil {
			return nil, err
		}
		return connack, nil
	}
}

func (c *SessionClient) signalConnection(
	ctx context.Context,
	event *ConnectEvent,
) {
	c.log.Info(ctx, "connected",
		slog.Int("reason_code", int(event.ReasonCode)),
	)

	for handler := range c.connectEventHandlers.All() {
		handler(event)
	}
}

func (c *SessionClient) signalDisconnection(
	ctx context.Context,
	event *DisconnectEvent,
) {
	switch {
	case event.ReasonCode != nil:
		c.log.Warn(ctx, "disconnected",
			slog.Int("reason_code", int(*event.ReasonCode)),
		)

	case event.Error != nil:
		c.log.Warn(ctx, "disconnected",
			slog.String("error", event.Error.Error()),
		)

	default:
		c.log.Warn(ctx, "disconnected")
	}

	for handler := range c.disconnectEventHandlers.All() {
		handler(event)
	}
}

func (c *SessionClient) forceDisconnect(
	ctx context.Context,
	client PahoClient,
) {
	immediateSessionExpiry := uint32(0)
	disconn := &paho.Disconnect{
		ReasonCode: disconnectNormalDisconnection,
		Properties: &paho.DisconnectProperties{
			SessionExpiryInterval: &immediateSessionExpiry,
		},
	}
	c.log.Packet(ctx, "disconnect", disconn)
	_ = client.Disconnect(disconn)
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

func (c *SessionClient) buildConnectPacket(
	ctx context.Context,
	reconnect bool,
) (*paho.Connect, error) {
	sessionExpiryInterval := c.config.sessionExpiryInterval
	properties := paho.ConnectProperties{
		SessionExpiryInterval: &sessionExpiryInterval,
		ReceiveMaximum:        &c.config.receiveMaximum,
		RequestProblemInfo:    true,
		User: internal.MapToUserProperties(
			c.config.userProperties,
		),
	}

	packet := &paho.Connect{
		ClientID:   c.config.clientID,
		CleanStart: !reconnect && c.config.firstConnectionCleanStart,
		KeepAlive:  c.config.keepAlive,
		Properties: &properties,
	}

	userName, userNameFlag, err := c.config.userNameProvider(ctx)
	if err != nil {
		return nil, &InvalidArgumentError{
			wrapped: err,
			message: "error getting user name from UserNameProvider",
		}
	}
	if userNameFlag {
		packet.UsernameFlag = true
		packet.Username = userName
	}

	password, passwordFlag, err := c.config.passwordProvider(ctx)
	if err != nil {
		return nil, &InvalidArgumentError{
			wrapped: err,
			message: "error getting password from PasswordProvider",
		}
	}
	if passwordFlag {
		packet.PasswordFlag = true
		packet.Password = password
	}

	if c.config.authProvider != nil {
		authValues, err := c.config.authProvider.InitiateAuthExchange(false)
		if err != nil {
			return nil, &InvalidArgumentError{
				wrapped: err,
				message: "error getting auth values from EnhancedAuthenticationProvider",
			}
		}
		packet.Properties.AuthData = authValues.AuthenticationData
		packet.Properties.AuthMethod = authValues.AuthenticationMethod
	}

	return packet, nil
}
