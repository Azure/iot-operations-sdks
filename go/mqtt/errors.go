// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"fmt"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
)

// ClientState indicates the current state of the session client.
type ClientState byte

const (
	// The session client has not yet been started.
	NotStarted ClientState = iota

	// The session client has been started and has not yet been stopped by the
	// user or terminated due to a fatal error.
	Started

	// The session client has been stopped by the user or terminated due to a
	// fatal error.
	ShutDown
)

// ClientStateError is returned when the operation cannot proceed due to the
// state of the session client.
type ClientStateError struct {
	State ClientState
}

func (e *ClientStateError) Error() string {
	switch e.State {
	case NotStarted:
		return "the session client has not yet been started"
	case Started:
		return "the session client has already been started"
	case ShutDown:
		return "the session client has been shut down"
	default:
		// It should not be possible to get here.
		return ""
	}
}

// DisconnectError indicates that the session client received a DISCONNECT
// packet from the server with a reason code that is not deemed to be fatal.
// It is primarily used for internal tracking and should not be expected by
// users except in rare cases in logs.
type DisconnectError struct {
	ReasonCode byte
}

func (e *DisconnectError) Error() string {
	return fmt.Sprintf(
		"received DISCONNECT packet with reason code 0x%x",
		e.ReasonCode,
	)
}

// FatalDisconnectError indicates that the session client has terminated due
// to receiving a DISCONNECT packet from the server with a reason code that
// is deemed to be fatal.
type FatalDisconnectError struct {
	ReasonCode byte
}

func (e *FatalDisconnectError) Error() string {
	return fmt.Sprintf(
		"received DISCONNECT packet with fatal reason code 0x%x",
		e.ReasonCode,
	)
}

// SessionLostError indicates that the session client has terminated due to
// receiving a CONNACK with session present false when reconnecting.
type SessionLostError struct{}

func (*SessionLostError) Error() string {
	return "expected server to have session information, but received a CONNACK packet with session present false"
}

// ConnectionError indicates that the session client has terminated due to an
// issue opening the network connection to the MQTT server. It may wrap an
// underlying error using Go standard error wrapping.
type ConnectionError struct {
	message string
	wrapped error
}

func (e *ConnectionError) Error() string {
	if e.wrapped != nil {
		return fmt.Sprintf("%s: %v", e.message, e.wrapped)
	}
	return e.message
}

func (e *ConnectionError) Unwrap() error {
	return e.wrapped
}

// ConnackError indicates that the session client received a CONNACK with a
// reason code that indicates an error but is not deemed to be fatal. It may
// appear as a fatal error if it is the final error returned once the session
// client has exhausted its connection retries.
type ConnackError struct {
	ReasonCode byte
}

func (e *ConnackError) Error() string {
	return fmt.Sprintf(
		"received CONNACK packet with error reason code 0x%x",
		e.ReasonCode,
	)
}

// FatalConnackError indicates that the session client has terminated due to
// receiving a CONNACK with with a reason code that is deemed to be fatal.
type FatalConnackError struct {
	ReasonCode byte
}

func (e *FatalConnackError) Error() string {
	return fmt.Sprintf(
		"received CONNACK packet with fatal reason code 0x%x",
		e.ReasonCode,
	)
}

// InvalidArgumentError indicates that the user has provided an invalid value
// for an option. It may wrap an underlying error using Go standard error
// wrapping.
type InvalidArgumentError struct {
	message string
	wrapped error
}

func (e *InvalidArgumentError) Error() string {
	if e.wrapped != nil {
		return fmt.Sprintf("%s: %v", e.message, e.wrapped)
	}
	return e.message
}

func (e *InvalidArgumentError) Unwrap() error {
	return e.wrapped
}

// PublishQueueFullError is returned if there are too many publishes enqueued
// and the session client is not accepting any more. This should very rarely
// occur, and if it does, it is a sign that either the connection is unstable
// or the application is sending messages at a faster rate than can be handled
// by the session client or server.
type PublishQueueFullError struct{}

func (*PublishQueueFullError) Error() string {
	return "publish queue full"
}

// AIOBrokerFeatureError indicates that a feature specific to the AIO Broker was
// used when AIO Broker features were explicitly disabled.
type AIOBrokerFeatureError struct {
	feature string
}

func (e *AIOBrokerFeatureError) Error() string {
	return fmt.Sprintf(
		"%s was used with AIO Broker features disabled",
		e.feature,
	)
}

// HandlerPanicError indicates that a user-provided handler panicked. This error
// will never be returned, only logged.
type HandlerPanicError struct {
	panic any
}

func (e *HandlerPanicError) Error() string {
	return fmt.Sprintf("panic in user-provided handler: %v", e.panic)
}

func catchHandlerPanic(log internal.Logger) {
	if e := recover(); e != nil {
		log.Error(context.Background(), &HandlerPanicError{e})
	}
}
