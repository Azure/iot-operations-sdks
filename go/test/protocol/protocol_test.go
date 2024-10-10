package protocol

import (
	"fmt"
	"testing"
)

func TestCommandInvoker(t *testing.T) {
	fmt.Printf("Running TestCommandInvoker\n")
	RunCommandInvokerTests(t)
}

func TestCommandExecutor(t *testing.T) {
	fmt.Printf("Running TestCommandExecutor\n")
	RunCommandExecutorTests(t)
}
