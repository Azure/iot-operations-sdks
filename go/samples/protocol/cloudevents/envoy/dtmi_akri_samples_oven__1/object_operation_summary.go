// Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT.
package dtmi_akri_samples_oven__1

import (
	"github.com/Azure/iot-operations-sdks/go/protocol/iso"
)

type Object_OperationSummary struct {
	NumberOfCakes *int64 `json:"numberOfCakes,omitempty"`
	StartingTime *iso.Time `json:"startingTime,omitempty"`
	TotalDuration *iso.Duration `json:"totalDuration,omitempty"`
}
