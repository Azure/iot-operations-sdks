// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package schemaregistry

import "github.com/Azure/iot-operations-sdks/go/services/schemaregistry/dtmi_ms_adr_SchemaRegistry__1"

// Schema represents the stored schema payload.
type Schema = dtmi_ms_adr_SchemaRegistry__1.Object_Ms_Adr_SchemaRegistry_Schema__1

// Format represents the encoding used to store the schema. It specifies how the
// schema content should be interpreted.
type Format = dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_Format__1

const (
	Delta1            = dtmi_ms_adr_SchemaRegistry__1.Delta1
	JSONSchemaDraft07 = dtmi_ms_adr_SchemaRegistry__1.JsonSchemaDraft07
)

// SchemaType represents the type of the schema.
type SchemaType = dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_SchemaType__1

const (
	MessageSchema = dtmi_ms_adr_SchemaRegistry__1.MessageSchema
)
