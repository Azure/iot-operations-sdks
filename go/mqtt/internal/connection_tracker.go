// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import (
	"context"
	"iter"
	"sync"
)

type (
	// Struct to track the connection state of the client, and retreive the
	// currently connected client.
	ConnectionTracker[Client comparable] struct {
		current   CurrentConnection[Client]
		currentMu sync.RWMutex
	}

	// Mutex-protected connection data.
	CurrentConnection[Client comparable] struct {
		// Current instance of the client.
		Client Client

		// Channel that is closed when the connection is up (i.e., a new client
		// instance is created and connected to the server with a successful
		// CONNACK), used to notify goroutines that are waiting on a connection
		// to be re-established.
		Up chan struct{}

		// Channel that is closed when the the connection is down. Used to
		// notify goroutines that expect the connection to go down that the
		// manageConnection() goroutine has detected the disconnection and is
		// attempting to start a new connection.
		Down chan struct{}

		// The number of successful connections that have ocurred on the session
		// client, up to and including the current client instance.
		Count uint64

		// InflightReauth is true iff there is an MQTT Enhanced Authentication
		// exchange in progress for re-authentication.
		InflightReauth bool
	}
)

func NewConnectionTracker[Client comparable]() *ConnectionTracker[Client] {
	c := &ConnectionTracker[Client]{}
	c.current.Up = make(chan struct{})
	c.current.Down = make(chan struct{})

	// Immediately close Down to maintain the invariant that Down is closed iff
	// the client is disconnected.
	close(c.current.Down)

	return c
}

func (c *ConnectionTracker[Client]) RequestReauthentication() {
	c.currentMu.Lock()
	defer c.currentMu.Unlock()

	var zero Client
	if c.current.Client == zero || c.current.InflightReauth {
		return
	}

}

func (c *ConnectionTracker[Client]) Connect(client Client) {
	c.currentMu.Lock()
	defer c.currentMu.Unlock()

	c.current.Client = client
	close(c.current.Up)
	c.current.Down = make(chan struct{})
	c.current.Count++
}

func (c *ConnectionTracker[Client]) Disconnect() {
	c.currentMu.Lock()
	defer c.currentMu.Unlock()

	var zero Client
	c.current.Client = zero
	c.current.Up = make(chan struct{})
	close(c.current.Down)
}

func (c *ConnectionTracker[Client]) Current() CurrentConnection[Client] {
	c.currentMu.RLock()
	defer c.currentMu.RUnlock()

	return c.current
}

// Return the client for the current connection. Since the client gets replaced
// when the we reconnect, this is represented as an iterator. The caller should
// return from the loop once the call they're trying to make is complete, or
// continue the loop if we need to reconnect and try again. The loop will only
// terminate on its own via the context.
func (c *ConnectionTracker[Client]) Client(
	ctx context.Context,
) iter.Seq2[Client, <-chan struct{}] {
	return func(yield func(Client, <-chan struct{}) bool) {
		for {
			current := c.Current()

			var zero Client
			if current.Client == zero {
				select {
				case <-ctx.Done():
					return
				case <-current.Up:
					continue
				}
			}

			if !yield(current.Client, current.Down) {
				return
			}

			// If we get here, the request failed because the connection is down
			// or because ctx was cancelled.
			select {
			case <-ctx.Done():
				return
			case <-current.Down:
				// Connection is down, wait for the connection to come back up
				// and retry.
			}
		}
	}
}
