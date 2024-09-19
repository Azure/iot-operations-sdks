package internal

import "fmt"

// Error represents an error in a state store method.
type Error struct {
	Operation string
	Message   string
	Value     string
}

// Error returns a stringified state store error.
func (e *Error) Error() string {
	if e.Value == "" {
		return fmt.Sprintf("%s: %s", e.Operation, e.Message)
	}
	return fmt.Sprintf("%s: %s: %s", e.Operation, e.Message, e.Value)
}
