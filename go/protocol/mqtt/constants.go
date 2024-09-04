package mqtt

type (
	QoS            byte
	RetainHandling byte
	PayloadFormat  byte
)

// Quality of Service levels.
const (
	// QoS0 indicates at most once delivery, a.k.a. "fire and forget".
	QoS0 QoS = iota

	// QoS1 indicates at least once delivery, which ensures the message is
	// delivered at least one time to the receiver.
	QoS1

	// QoS2 indicates exactly once delivery, which ensures the message is
	// received only once by the recipient.
	QoS2
)

// Retain Handling options.
const (
	// RetainHandling0 indicates that the Server MUST send the retained messages
	// matching the Topic Filter of the subscription to the Client.
	RetainHandling0 RetainHandling = iota

	// RetainHandling1 indicates that if the subscription did not already exist,
	// the Server MUST send all retained messages matching the Topic Filter of
	// the subscription to the Client, and if the subscription did exist the
	// Server MUST NOT send the retained messages.
	RetainHandling1

	// RetainHandling2 indicates that the Server MUST NOT send the retained
	// messages.
	RetainHandling2
)

// Payload Format indicators.
const (
	// PayloadFormat0 indicates that the payload is unspecified bytes.
	PayloadFormat0 PayloadFormat = iota

	// PayloadFormat1 indicates that the payload is UTF-8 encoded character
	// data.
	PayloadFormat1
)
