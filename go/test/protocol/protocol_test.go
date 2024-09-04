package protocol

import (
	"fmt"
	"testing"
)

func TestCommandInvokerStandalone(t *testing.T) {
	fmt.Printf("Running TestCommandInvoker\n")
	RunCommandInvokerTests(t, false)
}

func TestCommandInvokerWithSessionClient(t *testing.T) {
	fmt.Printf("Running TestCommandInvoker\n")
	RunCommandInvokerTests(t, true)
}

func TestCommandExecutorStandalone(t *testing.T) {
	fmt.Printf("Running TestCommandExecutor\n")
	RunCommandExecutorTests(t, false)
}

func TestCommandExecutorWithSessionClient(t *testing.T) {
	fmt.Printf("Running TestCommandExecutor\n")
	RunCommandExecutorTests(t, true)
}
