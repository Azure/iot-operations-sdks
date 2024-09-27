package mqtt

import (
	"context"
	"fmt"
	"math"
	"sync"

	"github.com/eclipse/paho.golang/paho"

	"github.com/Azure/iot-operations-sdks/go/mqtt/retrypolicy"
	protocolErrors "github.com/Azure/iot-operations-sdks/go/protocol/errors"
)

// Attempts an initial connection and then listens for disconnections
// to attempt reconnections. Blocks until the ctx is cancelled or
// the connection can no longer be maintained (due to a fatal error
// or retry policy exhaustion)
func (c *SessionClient) manageConnection(ctx context.Context) error {
	var pahoClient PahoClient
	var disconnected <-chan error

	signalDisconnection := func() {
		// caller must hold a write lock on c.pahoClientMu
		c.pahoClient = nil
		c.connUp = make(chan struct{})
		close(c.connDown)
	}

	// On cleanup, send a DISCONNECT packet if possible and signal a disconnection to other goroutines if needed.
	defer func() {
		c.pahoClientMu.Lock()
		defer c.pahoClientMu.Unlock()
		if c.pahoClient == nil {
			return
		}
		// TODO: allow the user to specify their own DISCONNECT packet (i.e., customize the reason code or properties)
		immediateSessionExpiry := uint32(0)
		_ = c.pahoClient.Disconnect(&paho.Disconnect{
			ReasonCode: byte(disconnectNormalDisconnection),
			Properties: &paho.DisconnectProperties{
				SessionExpiryInterval: &immediateSessionExpiry,
			},
		})
		signalDisconnection()
	}()

	for {
		isInitialConn := c.connCount == 0 // this is thread safe because this is the only goroutine that is supposed to write to connCount
		err := c.connRetry.Start(
			ctx,
			c.info,
			retrypolicy.Task{
				Name: "connect",
				Exec: func(ctx context.Context) error {
					var err error
					pahoClient, disconnected, err = c.buildPahoClient(ctx, isInitialConn)
					return err
				},
				Cond: isRetryableError,
			},
		)
		if err != nil {
			// TODO: ensure error is normalized correctly
			return err
		}

		c.pahoClientMu.Lock()
		c.pahoClient = pahoClient
		close(c.connUp) // TODO: we can use connup to get notified when the first connection succeeds
		c.connDown = make(chan struct{})
		c.connCount++
		c.pahoClientMu.Unlock()

		select {
		case err = <-disconnected:
			// Current paho instance got disconnected
		case <-ctx.Done():
			// SessionClient is shutting down
			return nil
		}

		// TODO: check if this is a fatal error, return normalized error if so

		c.pahoClientMu.Lock()
		signalDisconnection()
		c.pahoClientMu.Unlock()

		// if we get here, a reconnection will be attempted
	}
}

// Run starts the SessionClient and blocks until the SessionClient has stopped. ctx cancellation is used to stop the SessionClient.
// If the SessionClient terminates due to an error, an error will be be returned. A SessionClient instance may only be run once.
func (c *SessionClient) Run(ctx context.Context) error {
	if !c.sessionStarted.CompareAndSwap(false, true) {
		// TODO: normalize error
		return fmt.Errorf("Run() already called on this SessionClient instance")
	}

	clientShutdownCtx, clientShutdownFunc := context.WithCancel(context.Background())

	// buffered by 1 to ensure the SessionClient doesn't hang if manageConnection produces an error after ctx is cancelled
	errChan := make(chan error, 1)

	c.wg.Add(1)
	go func() {
		errChan <- c.manageConnection(clientShutdownCtx)
		c.wg.Done()
	}()

	c.wg.Add(1)
	go func() {
		c.manageOutgoingPublishes(clientShutdownCtx)
		c.wg.Done()
	}()

	var err error
	select {
	case <-ctx.Done():
	case err = <-errChan:
	}

	close(c.shutdown)
	clientShutdownFunc()
	c.wg.Wait()

	return err
}

// buildPahoClient creates an instance of a Paho client and attempts to connect it to the server. If the client is successfully connected,
// the client instance is returned along with a channel to be notified when the connection on that client instance goes down.
func (c *SessionClient) buildPahoClient(ctx context.Context, isInitialConn bool) (PahoClient, <-chan error, error) {
	// Refresh TLS config for new connection.
	if err := c.connSettings.validateTLS(); err != nil {
		// TODO: this currently returns immediately if refreshing TLS config fails. Do we want to instead attempt to connect with the stale TLS config?
		return nil, nil, err
	}

	conn, err := buildNetConn(
		ctx,
		c.connSettings.serverURL,
		c.connSettings.tlsConfig,
	)
	if err != nil {
		// buildNetConn will wrap the error in retryableErr if it's retryable
		return nil, nil, err
	}

	// buffer the channel by 1 to avoid hanging a goroutine in the case where paho has an error AND the server sends a DISCONNECT packet.
	disconnected := make(chan error, 1)
	var clientErrOnce, serverDisconnectOnce sync.Once

	pahoClient := paho.NewClient(paho.ClientConfig{
		ClientID:    c.connSettings.clientID,
		Conn:        conn,
		Session:     c.session,
		AuthHandler: c.connSettings.authOptions.AuthHandler,
		// TODO: put the callback here once we refactor the session client to only have one callback registered in paho
		OnPublishReceived: nil,
		// Set Paho's packet timeout to the maximum possible value to effectively disable it. The session client will control any needed timeouts instead.
		PacketTimeout: math.MaxInt64,
		OnServerDisconnect: func(d *paho.Disconnect) {
			serverDisconnectOnce.Do(func() {
				disconnected <- disconnErr.Translate(context.Background(), d, nil)
			})
		},
		OnClientError: func(err error) {
			clientErrOnce.Do(func() {
				disconnected <- err
			})
		},
		// Disable automatic acking in Paho. The session client will manage acks instead.
		EnableManualAcknowledgment: true,
	})

	// TODO: make this just one callback and manage callbacks in the session client.
	for _, s := range c.subscriptions {
		s.register(ctx)
	}

	if c.connSettings.authOptions.AuthDataProvider != nil {
		WithAuthData(c.connSettings.authOptions.AuthDataProvider(ctx))(c)
	}

	cp := buildConnectPacket(c.connSettings.clientID, c.connSettings, isInitialConn)

	c.logConnect(cp)
	// TODO: figure out results from paho map to the return values of pahoconn
	// TODO: timeout if CONNACK doesn't come back in a reasonable amount of time
	connack, err := pahoConn(ctx, pahoClient, cp) // TODO: determine if we want to call pahoConn or conn on the client directly.

	if connack != nil && connack.ReasonCode >= 0x80 && !isRetryableConnack(reasonCode(connack.ReasonCode)) {
		return nil, nil, err
	}
	if err != nil {
		// TODO: this assumes that all errors returned by Paho's connect method without a CONNACK are retryable.
		return nil, nil, retryableErr{err}
	}
	if !isInitialConn && !connack.SessionPresent {
		return nil, nil, &protocolErrors.Error{
			Message: "mqtt server sent a connack with session present false when a session was expected",
			Kind:    protocolErrors.MqttError,
		}
	}

	return pahoClient, disconnected, nil
}
