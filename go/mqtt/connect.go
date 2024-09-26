package mqtt

import (
	"context"
	"fmt"
	"math"
	"sync"
	"sync/atomic"

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

// Disconnect closes the connection gracefully
// by sending the disconnect packet to server
// and should terminate any active goroutines before returning.
func (c *SessionClient) Disconnect() error {
	if err := c.ensureClient(); err != nil {
		return err
	}

	if err := c.ensurePahoClient(); err != nil {
		return err
	}

	if !c.isConnected.Load() {
		return &protocolErrors.Error{
			Kind:    protocolErrors.StateInvalid,
			Message: "Cannot disconnect since the client is not connected",
		}
	}

	c.info("start disconnection")

	// Exit all background goroutines.
	close(c.connStopC.C)

	// Sending disconnect packet to server.
	disconnErr := c.attemptDisconnect()
	if disconnErr != nil {
		c.error(fmt.Sprintf(
			"an error ocurred during disconnection: %s",
			disconnErr.Error(),
		))
		return disconnErr
	}

	c.info("disconnected")

	return nil
}

func (c *SessionClient) attemptDisconnect() error {
	dp := buildDisconnectPacket(
		disconnectNormalDisconnection,
		"connection context cancellation",
	)
	c.logDisconnect(dp)
	return pahoDisconn(c.pahoClient, dp)
}

// bufferPacket adds a packet to the queue and waits for future reconnection.
func (c *SessionClient) bufferPacket(
	ctx context.Context,
	pq *queuedPacket,
) error {
	c.info(fmt.Sprintf(
		"connection lost; buffer packet: %#v",
		pq.packet,
	))

	if c.pendingPackets.IsFull() {
		return &protocolErrors.Error{
			Kind: protocolErrors.ExecutionException,
			Message: fmt.Sprintf(
				"%s cannot be enqueued as the queue is full",
				pq.packetType(),
			),
		}
	}

	pq.errC = make(chan error, 1)
	c.pendingPackets.Enqueue(*pq)

	// Blocking until we get expected response from reconnection.
	c.info("waiting for packet response after reconnection")
	select {
	case err, ok := <-pq.errC:
		if ok {
			return err
		}
		return nil
	case <-ctx.Done():
		return &protocolErrors.Error{
			Kind: protocolErrors.StateInvalid,
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
	c.info("start processing pending packets after reconnection")
	if c.pendingPackets.IsEmpty() {
		c.info("no pending packets in the queue")
	}

	for !c.pendingPackets.IsEmpty() {
		c.info(
			fmt.Sprintf("%d packet(s) in the queue", c.pendingPackets.Size()),
		)
		qp := c.pendingPackets.Dequeue()
		if qp != nil {
			switch p := qp.packet.(type) {
			case *paho.Publish:
				c.logPacket(p)
				qp.handleError(pahoPub(ctx, c.pahoClient, p))
			case *paho.Subscribe:
				c.logPacket(p)

				qp.subscription.register(ctx)
				err := pahoSub(ctx, c.pahoClient, p)
				if err == nil {
					c.subscriptions[qp.subscription.topic] = qp.subscription
				} else {
					qp.subscription.done()
				}

				qp.handleError(err)
			case *paho.Unsubscribe:
				c.logPacket(p)

				err := pahoUnsub(ctx, c.pahoClient, p)
				if err == nil {
					// Remove subscribed topic and subscription callback.
					delete(c.subscriptions, qp.subscription.topic)
					qp.subscription.done()
				}

				qp.handleError(err)
			default:
				c.error(
					fmt.Sprintf(
						"cannot process unknown packet in queue: %v",
						qp,
					),
				)
				continue
			}
		}
	}

	// Unblock other operations.
	c.info("pending packets processing completes; resume other operations")
	c.packetQueueCond.Broadcast()
}

// buildPahoClient creates an instance of a Paho client and attempts to connect it to the server.
// if the client is successfully connected, the client instance is returned along with a channel to be notified
// if the connection on that client instance goes down.
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

// prepare validates the connection status and packet queue
// before sending subscribe/unsubscribe/publish packets.
func (c *SessionClient) prepare(ctx context.Context) error {
	if err := c.ensureClient(); err != nil {
		return err
	}

	if err := c.ensurePahoClient(); err != nil {
		return err
	}

	// Initial connection failed after retries, so no responses are expected
	// due to the lack of recovery.
	if !c.isConnected.Load() && atomic.LoadUint64(&c.connCount) < 1 {
		err := &protocolErrors.Error{
			Kind:        protocolErrors.StateInvalid,
			Message:     "no initial connection; request cannot be executed",
			NestedError: ctx.Err(),
		}
		c.error(err.Error())
		return err
	}

	// The operation will block if the connection is up and
	// there are pending packets in the queue.
	// If the connection is down, packets will be added to the queue directly.
	// Users can spawn a goroutine to call the function and unblock their codes.
	for !c.pendingPackets.IsEmpty() && c.isConnected.Load() {
		c.info("pending packets in the queue; wait for the process")
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
func (c *SessionClient) _shutdown(err error) {
	c.info("client is shutting down")

	c.setDisconnected()
	c.closeClientErrC()
	c.closeDisconnErrC()

	c.shutdownHandler(err)
}

func (c *SessionClient) onClientError(err error) {
	if !c.isConnected.Load() {
		return
	}

	c.info("an error from onClientError occurs")
	c.setDisconnected()

	if err != nil && !c.clientErrC.Send(err) {
		c.error(
			fmt.Sprintf(
				"failed to send error from onClientError; "+
					"internal channel closed: %s",
				err.Error(),
			),
		)
	}
}

func (c *SessionClient) onServerDisconnect(disconnect *paho.Disconnect) {
	if !c.isConnected.Load() {
		return
	}

	c.info("server sent a disconnect packet")

	c.setDisconnected()

	var err error
	if disconnect != nil &&
		isRetryableDisconnect(reasonCode(disconnect.ReasonCode)) {
		err = disconnErr.Translate(context.Background(), disconnect, nil)
	}

	if err != nil && !c.disconnErrC.Send(err) {
		c.error(
			fmt.Sprintf(
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

func (c *SessionClient) closeClientErrC() {
	c.clientErrC.Close()
}

func (c *SessionClient) closeDisconnErrC() {
	c.disconnErrC.Close()
}
