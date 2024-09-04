package mqtt

import (
	"math"
	"time"
)

type reasonCode byte

const (
	defaultReceiveMaximum    uint16        = math.MaxUint16
	maxKeepAlive             uint16        = math.MaxUint16
	maxSessionExpiry         uint32        = math.MaxUint32
	maxPacketQueueSize       int           = math.MaxUint16
	maxInitialConnectRetries int           = 5
	defaultAuthInterval      time.Duration = 20 * time.Minute
	aesGcmNonce              int           = 12
)

const (
	subscribePacket   string = "subscribe"
	unsubscribePacket string = "unsubscribe"
	publishPacket     string = "publish"
)

// CONNACK reason codes.
const (
	connackSuccess                     reasonCode = 0x00
	connackNotAuthorized               reasonCode = 0x87
	connackServerUnavailable           reasonCode = 0x88
	connackServerBusy                  reasonCode = 0x89
	connackQuotaExceeded               reasonCode = 0x97
	connackConnectionRateExceeded      reasonCode = 0x9F
	connackMalformedPacket             reasonCode = 0x81
	connackProtocolError               reasonCode = 0x82
	connackImplementationSpecificError reasonCode = 0x83
	connackUnsupportedProtocolVersion  reasonCode = 0x84
	connackBadAuthenticationMethod     reasonCode = 0x8C
	connackClientIdentifierNotValid    reasonCode = 0x85
	connackBadUserNameOrPassword       reasonCode = 0x86
	connackBanned                      reasonCode = 0x8A
	connackUseAnotherServer            reasonCode = 0x93
	connackReauthenticate              reasonCode = 0x19
)

// DISCONNECT reason codes.
const (
	disconnectNormalDisconnection                 reasonCode = 0x00
	disconnectNotAuthorized                       reasonCode = 0x87
	disconnectServerUnavailable                   reasonCode = 0x88
	disconnectServerBusy                          reasonCode = 0x89
	disconnectQuotaExceeded                       reasonCode = 0x97
	disconnectConnectionRateExceeded              reasonCode = 0x9F
	disconnectMalformedPacket                     reasonCode = 0x81
	disconnectProtocolError                       reasonCode = 0x82
	disconnectBadAuthenticationMethod             reasonCode = 0x8C
	disconnectSessionTakenOver                    reasonCode = 0x8D
	disconnectTopicFilterInvalid                  reasonCode = 0x8E
	disconnectTopicNameInvalid                    reasonCode = 0x8F
	disconnectTopicAliasInvalid                   reasonCode = 0x90
	disconnectPacketTooLarge                      reasonCode = 0x95
	disconnectPayloadFormatInvalid                reasonCode = 0x99
	disconnectRetainNotSupported                  reasonCode = 0x9A
	disconnectQoSNotSupported                     reasonCode = 0x9B
	disconnectServerMoved                         reasonCode = 0x9D
	disconnectSharedSubscriptionsNotSupported     reasonCode = 0x9E
	disconnectSubscriptionIdentifiersNotSupported reasonCode = 0xA1
	disconnectWildcardSubscriptionsNotSupported   reasonCode = 0xA2
)

// AUTH reason codes.
const (
	continueAuthentication reasonCode = 0x18
	reauthenticate         reasonCode = 0x19
)
