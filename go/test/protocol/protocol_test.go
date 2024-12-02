// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
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

func TestTelemetrySender(t *testing.T) {
	fmt.Printf("Running TestTelemetrySender\n")
	RunTelemetrySenderTests(t)
}

func TestTelemetryReceiver(t *testing.T) {
	fmt.Printf("Running TestTelemetryReceiver\n")
	RunTelemetryReceiverTests(t)
}
