// Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT.
package adrbaseservice

type AssetDataPointSchemaElementSchema struct {

	// The 'dataPointConfiguration' Field.
	DataPointConfiguration *string `json:"dataPointConfiguration,omitempty"`

	// The 'dataSource' Field.
	DataSource string `json:"dataSource"`

	// The 'name' Field.
	Name string `json:"name"`

	// The 'observabilityMode' Field.
	ObservabilityMode *AssetDataPointObservabilityModeSchema `json:"observabilityMode,omitempty"`
}
