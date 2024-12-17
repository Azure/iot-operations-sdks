// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

// User property keys.
const (
	SourceID             = "__srcId"
	Timestamp            = "__ts"
	FencingToken         = "__ft"
	Status               = "__stat"
	StatusMessage        = "__stMsg"
	IsApplicationError   = "__apErr"
	InvalidPropertyName  = "__propName"
	InvalidPropertyValue = "__propVal"
)

// Standard names for MQTT properties.
const (
	ContentType     = "Content Type"
	FormatIndicator = "Payload Format Indicator"
	CorrelationData = "Correlation Data"
	ResponseTopic   = "Response Topic"
)
