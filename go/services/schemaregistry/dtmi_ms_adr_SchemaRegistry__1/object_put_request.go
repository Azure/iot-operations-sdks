// Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT.
package dtmi_ms_adr_SchemaRegistry__1

type Object_Put_Request struct {
	Description *string `json:"description,omitempty"`
	DisplayName *string `json:"displayName,omitempty"`
	Format *Enum_Ms_Adr_SchemaRegistry_Format__1 `json:"format,omitempty"`
	SchemaContent *string `json:"schemaContent,omitempty"`
	SchemaType *Enum_Ms_Adr_SchemaRegistry_SchemaType__1 `json:"schemaType,omitempty"`
	Tags map[string]string `json:"tags,omitempty"`
	Version *string `json:"version,omitempty"`
}
