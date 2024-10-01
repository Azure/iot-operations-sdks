package mqtt

import (
	"context"
	"math"
	"sync"

	"github.com/Azure/iot-operations-sdks/go/mqtt/retrypolicy"
	"github.com/eclipse/paho.golang/paho"
)

// Run starts the SessionClient and blocks until the SessionClient has stopped. ctx cancellation is used to stop the SessionClient.
// If the SessionClient terminates due to an error, an error will be be returned. A SessionClient instance may only be run once.
func (c *SessionClient) Run(ctx context.Context) error {
	if !c.sessionStarted.CompareAndSwap(false, true) {
		return &RunAlreadyCalledError{}
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

type pahoClientDisconnectedEvent struct {
	err              error
	disconnectPacket *paho.Disconnect
}

// Attempts an initial connection and then listens for disconnections to attempt reconnections. Blocks until the ctx is cancelled or
// the connection can no longer be maintained (due to a fatal error or retry policy exhaustion)
func (c *SessionClient) manageConnection(ctx context.Context) error {
	signalConnection := func(client PahoClient) {
		c.pahoClientMu.Lock()
		defer c.pahoClientMu.Unlock()

		c.pahoClient = client
		close(c.connUp)
		c.connDown = make(chan struct{})
		c.connCount++
	}

	signalDisconnection := func() {
		c.pahoClientMu.Lock()
		defer c.pahoClientMu.Unlock()

		c.pahoClient = nil
		c.connUp = make(chan struct{})
		close(c.connDown)
	}

	// On cleanup, send a DISCONNECT packet if possible and signal a disconnection to other goroutines if needed.
	defer func() {
		// NOTE: accessing c.pahoClient is thread safe here because manageConnection is no longer writing to c.pahoClient
		// if we get to this deferred function.
		if c.pahoClient == nil {
			return
		}
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
		var pahoClient PahoClient
		var disconnected <-chan *pahoClientDisconnectedEvent
		err := c.connRetry.Start(
			ctx,
			c.info,
			retrypolicy.Task{
				Name: "connect",
				Exec: func(ctx context.Context) error {
					var err error
					pahoClient, disconnected, err = c.buildPahoClient(ctx, c.connCount)
					return err
				},
				Cond: isRetryableError,
			},
		)
		if err != nil {
			return &RetryFailureError{LastError: err}
		}
		signalConnection(pahoClient)

		var disconnectEvent *pahoClientDisconnectedEvent
		select {
		case disconnectEvent = <-disconnected:
			// Current paho instance got disconnected
		case <-ctx.Done():
			// SessionClient is shutting down
			return nil
		}

		if disconnectEvent.disconnectPacket != nil && !isRetryableDisconnect(reasonCode(disconnectEvent.disconnectPacket.ReasonCode)) {
			return &FatalDisconnectError{ReasonCode: reasonCode(disconnectEvent.disconnectPacket.ReasonCode)}

		}
		signalDisconnection()

		// if we get here, a reconnection will be attempted
	}
}

// buildPahoClient creates an instance of a Paho client and attempts to connect it to the server. If the client is successfully connected,
// the client instance is returned along with a channel to be notified when the connection on that client instance goes down.
func (c *SessionClient) buildPahoClient(ctx context.Context, connCount uint64) (PahoClient, <-chan *pahoClientDisconnectedEvent, error) {
	isInitialConn := connCount == 0

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
	disconnected := make(chan *pahoClientDisconnectedEvent, 1)
	var clientErrOnce, serverDisconnectOnce sync.Once

	pahoClient := paho.NewClient(paho.ClientConfig{
		ClientID: c.connSettings.clientID,
		Conn:     conn,
		Session:  c.session,
		OnPublishReceived: []func(paho.PublishReceived) (bool, error){
			c.makeOnPublishReceived(connCount + 1), // add 1 to the conn count for this because this listener is effective AFTER the connection succeeds
		},
		PacketTimeout: math.MaxInt64, // Set Paho's packet timeout to the maximum possible value to effectively disable it. We can still control any timeouts through the contexts we pass into Paho.
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
		// Disable automatic acking in Paho. The session client will manage acks instead.
		EnableManualAcknowledgment: true,
	})

	cp := buildConnectPacket(c.connSettings.clientID, c.connSettings, isInitialConn)

	c.logConnect(cp)

	// TODO: timeout if CONNACK doesn't come back in a reasonable amount of time
	connack, err := pahoClient.Connect(ctx, cp)

	if connack != nil && connack.ReasonCode >= 0x80 && !isRetryableConnack(reasonCode(connack.ReasonCode)) {
		return nil, nil, err
	}
	if err != nil {
		// This assumes that all errors returned by Paho's connect method without a CONNACK are retryable.
		return nil, nil, retryableErr{err}
	}
	if !isInitialConn && !connack.SessionPresent {
		return nil, nil, &SessionLostError{}
	}

	return pahoClient, disconnected, nil
}
