// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package constants

// Protocol user property keys.
const (
	Protocol = "__"

	SourceID        = Protocol + "srcId"
	Timestamp       = Protocol + "ts"
	ProtocolVersion = Protocol + "protVer"

	Status                        = Protocol + "stat"
	StatusMessage                 = Protocol + "stMsg"
	IsApplicationError            = Protocol + "apErr"
	InvalidPropertyName           = Protocol + "propName"
	InvalidPropertyValue          = Protocol + "propVal"
	SupportedProtocolMajorVersion = Protocol + "supProtMajVer"
	RequestProtocolVersion        = Protocol + "requestProtVer"
)

// MQ user property keys.
const Partition = "$partition"

// Standard names for MQTT properties.
const (
	ContentType     = "Content Type"
	FormatIndicator = "Payload Format Indicator"
	CorrelationData = "Correlation Data"
	ResponseTopic   = "Response Topic"
	MessageExpiry   = "Message Expiry"
)
