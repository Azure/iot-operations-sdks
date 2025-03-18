// Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT.
package oven

type TelemetryCollection struct {

	// The 'externalTemperature' Telemetry.
	ExternalTemperature *float64 `json:"externalTemperature,omitempty"`

	// The 'internalTemperature' Telemetry.
	InternalTemperature *float64 `json:"internalTemperature,omitempty"`

	// The 'operationSummary' Telemetry.
	OperationSummary *OperationSummarySchema `json:"operationSummary,omitempty"`
}
