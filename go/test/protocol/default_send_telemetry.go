// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultSendTelemetry struct {
	TelemetryName  *string          `toml:"telemetry-name"`
	TelemetryValue *string          `toml:"telemetry-value"`
	Timeout        TestCaseDuration `toml:"timeout"`
	Qos            *int             `toml:"qos"`
}

func (sendTelemetry *DefaultSendTelemetry) GetTelemetryName() *string {
	if sendTelemetry.TelemetryName == nil {
		return nil
	}

	telemetryName := *sendTelemetry.TelemetryName
	return &telemetryName
}

func (sendTelemetry *DefaultSendTelemetry) GetTelemetryValue() *string {
	if sendTelemetry.TelemetryValue == nil {
		return nil
	}

	telemetryValue := *sendTelemetry.TelemetryValue
	return &telemetryValue
}

func (sendTelemetry *DefaultSendTelemetry) GetTimeout() TestCaseDuration {
	return sendTelemetry.Timeout
}

func (sendTelemetry *DefaultSendTelemetry) GetQos() *int {
	if sendTelemetry.Qos == nil {
		return nil
	}

	qos := *sendTelemetry.Qos
	return &qos
}
