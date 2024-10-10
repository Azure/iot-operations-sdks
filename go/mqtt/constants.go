package mqtt

import (
	"math"
)

const (
	defaultReceiveMaximum    uint16 = math.MaxUint16
	maxKeepAlive             uint16 = math.MaxUint16
	maxSessionExpiry         uint32 = math.MaxUint32
	maxPacketQueueSize       int    = math.MaxUint16
	maxInitialConnectRetries int    = 5
	aesGcmNonce              int    = 12
)

// CONNACK reason codes.
const (
	connackSuccess                     byte = 0x00
	connackNotAuthorized               byte = 0x87
	connackServerUnavailable           byte = 0x88
	connackServerBusy                  byte = 0x89
	connackQuotaExceeded               byte = 0x97
	connackConnectionRateExceeded      byte = 0x9F
	connackMalformedPacket             byte = 0x81
	connackProtocolError               byte = 0x82
	connackImplementationSpecificError byte = 0x83
	connackUnsupportedProtocolVersion  byte = 0x84
	connackBadAuthenticationMethod     byte = 0x8C
	connackClientIdentifierNotValid    byte = 0x85
	connackBadUserNameOrPassword       byte = 0x86
	connackBanned                      byte = 0x8A
	connackUseAnotherServer            byte = 0x93
	connackReauthenticate              byte = 0x19
)

// DISCONNECT reason codes.
const (
	disconnectNormalDisconnection                 byte = 0x00
	disconnectNotAuthorized                       byte = 0x87
	disconnectServerUnavailable                   byte = 0x88
	disconnectServerBusy                          byte = 0x89
	disconnectQuotaExceeded                       byte = 0x97
	disconnectConnectionRateExceeded              byte = 0x9F
	disconnectMalformedPacket                     byte = 0x81
	disconnectProtocolError                       byte = 0x82
	disconnectBadAuthenticationMethod             byte = 0x8C
	disconnectSessionTakenOver                    byte = 0x8D
	disconnectTopicFilterInvalid                  byte = 0x8E
	disconnectTopicNameInvalid                    byte = 0x8F
	disconnectTopicAliasInvalid                   byte = 0x90
	disconnectPacketTooLarge                      byte = 0x95
	disconnectPayloadFormatInvalid                byte = 0x99
	disconnectRetainNotSupported                  byte = 0x9A
	disconnectQoSNotSupported                     byte = 0x9B
	disconnectServerMoved                         byte = 0x9D
	disconnectSharedSubscriptionsNotSupported     byte = 0x9E
	disconnectSubscriptionIdentifiersNotSupported byte = 0xA1
	disconnectWildcardSubscriptionsNotSupported   byte = 0xA2
)
