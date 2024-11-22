// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultAction struct {
	InvokeCommand    DefaultInvokeCommand    `toml:"invoke-command"`
	SendTelemetry    DefaultSendTelemetry    `toml:"send-telemetry"`
	ReceiveRequest   DefaultReceiveRequest   `toml:"receive-request"`
	ReceiveResponse  DefaultReceiveResponse  `toml:"receive-response"`
	ReceiveTelemetry DefaultReceiveTelemetry `toml:"receive-telemetry"`
}
