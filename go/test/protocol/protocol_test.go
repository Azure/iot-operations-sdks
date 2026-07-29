// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"fmt"
	"testing"
)

func TestCommandInvoker(t *testing.T) {
	fmt.Println("Running TestCommandInvoker")
	RunCommandInvokerTests(t)
}

func TestCommandExecutor(t *testing.T) {
	fmt.Println("Running TestCommandExecutor")
	RunCommandExecutorTests(t)
}

func TestTelemetrySender(t *testing.T) {
	fmt.Println("Running TestTelemetrySender")
	RunTelemetrySenderTests(t)
}

func TestTelemetryReceiver(t *testing.T) {
	fmt.Println("Running TestTelemetryReceiver")
	RunTelemetryReceiverTests(t)
}
