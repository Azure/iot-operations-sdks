// Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT.
package schemaregistry

type PutRequestSchema struct {

	// Human-readable description of the schema.
	Description *string `json:"description,omitempty"`

	// Human-readable display name.
	DisplayName *string `json:"displayName,omitempty"`

	// Format of the schema.
	Format *Format `json:"format,omitempty"`

	// Content stored in the schema.
	SchemaContent *string `json:"schemaContent,omitempty"`

	// Type of the schema.
	SchemaType *SchemaType `json:"schemaType,omitempty"`

	// Schema tags.
	Tags map[string]string `json:"tags,omitempty"`

	// Version of the schema. Allowed between 0-9.
	Version *string `json:"version,omitempty"`
}
