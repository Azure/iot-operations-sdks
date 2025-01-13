// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package schemaregistry

import (
	"log/slog"
	"time"
)

type (
	// WithSchemaType specifies the type of this schema. Defaults to
	// MessageSchema.
	WithSchemaType SchemaType

	// WithTags specifies optional metadata tags to associate with the schema.
	WithTags map[string]string

	// WithTimeout adds a timeout to the request (with second precision).
	WithTimeout time.Duration

	// WithVersion specifies the semantic version of the schema. Defaults to
	// "1.0.0".
	WithVersion string

	// This option is not used directly; see WithLogger below.
	withLogger struct{ *slog.Logger }
)

// WithLogger enables logging with the provided slog logger.
func WithLogger(logger *slog.Logger) ClientOption {
	return withLogger{logger}
}
