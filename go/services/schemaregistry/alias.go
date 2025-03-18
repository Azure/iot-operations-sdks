// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package schemaregistry

import "github.com/Azure/iot-operations-sdks/go/services/schemaregistry/schemaregistry"

// Schema represents the stored schema payload.
type Schema = schemaregistry.Schema

// Format represents the encoding used to store the schema. It specifies how the
// schema content should be interpreted.
type Format = schemaregistry.Format

const (
	Delta1            = schemaregistry.Delta1
	JSONSchemaDraft07 = schemaregistry.JsonSchemaDraft07
)

// SchemaType represents the type of the schema.
type SchemaType = schemaregistry.SchemaType

const (
	MessageSchema = schemaregistry.MessageSchema
)
