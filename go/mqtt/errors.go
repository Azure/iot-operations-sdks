package mqtt

import "fmt"

type RunAlreadyCalledError struct{}

func (e *RunAlreadyCalledError) Error() string {
	return "Run() already called on this SessionClient instance"
}

type FatalDisconnectError struct {
	ReasonCode reasonCode
}

func (e *FatalDisconnectError) Error() string {
	return fmt.Sprintf("received DISCONNECT packet with fatal reason code %X", e.ReasonCode)
}

type SessionLostError struct{}

func (e *SessionLostError) Error() string {
	return "expected server have session information, but received a CONNACK packet with session present false"
}

type ConnackError struct {
	ReasonCode reasonCode
}

func (e *ConnackError) Error() string {
	return fmt.Sprintf("received CONNACK packet with error reason code %x", e.ReasonCode)
}

type RetryFailureError struct {
	LastError error
}

func (e *RetryFailureError) Error() string {
	return fmt.Sprintf("retries failed according to retry policy. last seen error: %v", e.LastError)
}

func (e *RetryFailureError) Unwrap() error {
	return e.LastError
}
