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

		// Error that caused the last disconnection.
		Error error

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

		// Counter for the current connection attempt. This is independent from
		// the client, since it also records unsuccessful connect attempts.
		Attempt uint64
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

func (c *ConnectionTracker[Client]) Attempt() uint64 {
	c.currentMu.Lock()
	defer c.currentMu.Unlock()

	c.current.Error = nil
	c.current.Attempt++
	return c.current.Attempt
}

func (c *ConnectionTracker[Client]) Connect(client Client) error {
	c.currentMu.Lock()
	defer c.currentMu.Unlock()

	// A disconnect was encountered between attempt and connect.
	// Don't connect and return the error.
	if c.current.Error != nil {
		return c.current.Error
	}

	c.current.Client = client
	close(c.current.Up)
	c.current.Down = make(chan struct{})
	return nil
}

func (c *ConnectionTracker[Client]) Disconnect(attempt uint64, err error) {
	c.currentMu.Lock()
	defer c.currentMu.Unlock()

	// This disconnect is for another attempt; don't change state.
	if c.current.Attempt != attempt {
		return
	}

	// Record the error if there isn't already one recorded.
	if c.current.Error == nil {
		c.current.Error = err
	}

	// An error was encountered before connect. Record it but don't disconnect.
	var zero Client
	if c.current.Client == zero {
		return
	}

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
