package mqtt

import "fmt"

/* ClientStateError */

const (
	// Run() has not yet been called on this SessionClient instance
	NotStarted = iota
	// Run() has been called on this SessionClient instance and it has not been
	// shut down
	Started
	// This SessionClient instance ran but was shut down due the user's request
	// or due to a fatal error
	ShutDown
)

// ClientStateError is returned when the operation cannot proceed due to the
// state of the SessionClient.
type ClientStateError struct {
	// Must be NotStarted, Started, or ShutDown
	State int
}

func (e *ClientStateError) Error() string {
	switch e.State {
	case NotStarted:
		return "Run() not yet called on this SessionClient instance"
	case Started:
		return "Run() already called on this SessionClient instance"
	case ShutDown:
		return "SessionClient has shut down"
	default:
		// it should not be possible to get here
		return ""
	}
}

/* FatalDisconnectError */

// FatalDisconnectError is returned by Run() if the SessionClient terminates due
// to receiving a DISCONNECT packet from the server with a reason code that is
// deemed to be fatal.
type FatalDisconnectError struct {
	// Must be set
	ReasonCode reasonCode
}

func (e *FatalDisconnectError) Error() string {
	return fmt.Sprintf("received DISCONNECT packet with fatal reason code %X", e.ReasonCode)
}

/* SessionLostError */

// SessionLostError is returned by Run() if the SessionClient terminates due to
// receiving a CONNACK from the server with session present false when
// reconnecting.
type SessionLostError struct{}

func (e *SessionLostError) Error() string {
	return "expected server have session information, but received a CONNACK packet with session present false"
}

/* ConnackError */

// ConnackError is returned by Run() if the SessionClient terminates due to
// receiving a CONNACK with an error reason code ConnackError is always wrapped
// by RetryFailureError, and may be checked using errors.As() from the Go
// standard library.
type ConnackError struct {
	// Must be set
	ReasonCode reasonCode
}

func (e *ConnackError) Error() string {
	return fmt.Sprintf("received CONNACK packet with error reason code %x", e.ReasonCode)
}

/* RetryFailureError */

// RetryFailureError is returned by Run() if the session client terminates due
// to reconnections failing and exhausting the retry policy. It wraps the last
// seen error using standard Go error wrapping.
type RetryFailureError struct {
	LastError error
}

func (e *RetryFailureError) Error() string {
	return fmt.Sprintf("retries failed according to retry policy. last seen error: %v", e.LastError)
}

func (e *RetryFailureError) Unwrap() error {
	if err, ok := e.LastError.(retryableErr); ok {
		return err.error
	}
	return e.LastError
}

/* InvalidArgumentError */

// InvalidArgumentError is used to indicate when the user has provided an
// invalid value for an option.
type InvalidArgumentError struct {
	// May or may not be set depending on whether there is actually an error to
	// wrap
	WrappedError error
	// Must be set
	message string
}

func (e *InvalidArgumentError) Error() string {
	if e.WrappedError != nil {
		return fmt.Sprintf("%s: %v", e.message, e.WrappedError)
	}
	return e.message
}

func (e *InvalidArgumentError) Unwrap() error {
	return e.WrappedError
}

/* PublishQueueFullError */

// PublishQueueFullError is returned by Publish() to indicate that there are too
// many publishes enqueued and the SessionClient is not accepting any more. This
// should very rarely occur, and if it does, it is a sign that either the
// connection is unstable or the application is sending messages at a faster
// rate than can be handled by the SessionClient or broker.
type PublishQueueFullError struct{}

func (e *PublishQueueFullError) Error() string {
	return "Publish queue full"
}
