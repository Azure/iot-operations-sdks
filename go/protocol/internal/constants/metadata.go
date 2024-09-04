package constants

// Protocol user property keys.
const (
	Protocol = "__"

	SenderClientID  = Protocol + "sndId"
	InvokerClientID = Protocol + "invId"
	Timestamp       = Protocol + "ts"
	FencingToken    = Protocol + "ft"

	Status               = Protocol + "stat"
	StatusMessage        = Protocol + "stMsg"
	IsApplicationError   = Protocol + "apErr"
	InvalidPropertyName  = Protocol + "propName"
	InvalidPropertyValue = Protocol + "propVal"
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
