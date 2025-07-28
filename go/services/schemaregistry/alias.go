// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package schemaregistry

import "github.com/Azure/iot-operations-sdks/go/services/schemaregistry/internal/schemaregistry"

// Schema represents the stored schema payload.
type Schema = schemaregistry.Schema

// Format represents the encoding used to store the schema. It specifies how the
// schema content should be interpreted.
type Format = schemaregistry.Format

const (
	Delta1            = schemaregistry.FormatDelta1
	JSONSchemaDraft07 = schemaregistry.FormatJsonSchemaDraft07
)

// SchemaType represents the type of the schema.
type SchemaType = schemaregistry.SchemaType

const (
	MessageSchema = schemaregistry.SchemaTypeMessageSchema
)

// Error object for schema operations.
type Error = schemaregistry.SchemaRegistryError

// ErrorCode for classification of errors (ex: '400', '404', '500', etc.).
type ErrorCode = schemaregistry.SchemaRegistryErrorCode

const (
	ErrorCodeBadRequest    = schemaregistry.SchemaRegistryErrorCodeBadRequest
	ErrorCodeInternalError = schemaregistry.SchemaRegistryErrorCodeInternalError
	ErrorCodeNotFound      = schemaregistry.SchemaRegistryErrorCodeNotFound
)

// ErrorDetails represents additional details about an error, if available.
type ErrorDetails = schemaregistry.SchemaRegistryErrorDetails

// ErrorTarget represents the target of the error, if applicable (e.g.,
// 'schemaType').
type ErrorTarget = schemaregistry.SchemaRegistryErrorTarget

const (
	ErrorTargetDescriptionProperty       = schemaregistry.SchemaRegistryErrorTargetDescriptionProperty
	ErrorTargetDisplayNameProperty       = schemaregistry.SchemaRegistryErrorTargetDisplayNameProperty
	ErrorTargetFormatProperty            = schemaregistry.SchemaRegistryErrorTargetFormatProperty
	ErrorTargetNameProperty              = schemaregistry.SchemaRegistryErrorTargetNameProperty
	ErrorTargetSchemaArmResource         = schemaregistry.SchemaRegistryErrorTargetSchemaArmResource
	ErrorTargetSchemaContentProperty     = schemaregistry.SchemaRegistryErrorTargetSchemaContentProperty
	ErrorTargetSchemaRegistryArmResource = schemaregistry.SchemaRegistryErrorTargetSchemaRegistryArmResource
	ErrorTargetSchemaTypeProperty        = schemaregistry.SchemaRegistryErrorTargetSchemaTypeProperty
	ErrorTargetSchemaVersionArmResource  = schemaregistry.SchemaRegistryErrorTargetSchemaVersionArmResource
	ErrorTargetTagsProperty              = schemaregistry.SchemaRegistryErrorTargetTagsProperty
	ErrorTargetVersionProperty           = schemaregistry.SchemaRegistryErrorTargetVersionProperty
)
