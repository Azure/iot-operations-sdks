// Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT.
package adrbaseservice

type DetectedAssetDatasetSchemaElementSchema struct {

	// The 'dataPoints' Field.
	DataPoints []DetectedAssetDataPointSchemaElementSchema `json:"dataPoints,omitempty"`

	// The 'dataSetConfiguration' Field.
	DataSetConfiguration *string `json:"dataSetConfiguration,omitempty"`

	// The 'name' Field.
	Name string `json:"name"`

	// The 'topic' Field.
	Topic *Topic `json:"topic,omitempty"`
}
