// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"math"
)

const (
	maxPublishQueueSize int = math.MaxUint16
	aesGcmNonce         int = 12
)

// CONNACK packet reason codes
// (https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901079)
const (
	connackSuccess                     byte = 0x00
	connackUnspecifiedError            byte = 0x80
	connackMalformedPacket             byte = 0x81
	connackProtocolError               byte = 0x82
	connackImplementationSpecificError byte = 0x83
	connackUnsupportedProtocolVersion  byte = 0x84
	connackClientIdentifierNotValid    byte = 0x85
	connackBadUserNameOrPassword       byte = 0x86
	connackNotAuthorized               byte = 0x87
	connackServerUnavailable           byte = 0x88
	connackServerBusy                  byte = 0x89
	connackBanned                      byte = 0x8A
	connackBadAuthenticationMethod     byte = 0x8C
	connackTopicNameInvalid            byte = 0x90
	connackPacketTooLarge              byte = 0x95
	connackQuotaExceeded               byte = 0x97
	connackPayloadFormatInvalid        byte = 0x99
	connackRetainNotSupported          byte = 0x9A
	connackQoSNotSupported             byte = 0x9B
	connackUseAnotherServer            byte = 0x9C
	connackServerMoved                 byte = 0x9D
	connackConnectionRateExceeded      byte = 0x9F
)

// DISCONNECT packet reason codes
// (https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901208)
const (
	disconnectNormalDisconnection                 byte = 0x00
	disconnectDisconnectWithWillMessage           byte = 0x04
	disconnectUnspecifiedError                    byte = 0x80
	disconnectMalformedPacket                     byte = 0x81
	disconnectProtocolError                       byte = 0x82
	disconnectImplementationSpecificError         byte = 0x83
	disconnectNotAuthorized                       byte = 0x87
	disconnectServerBusy                          byte = 0x89
	disconnectServerShuttingDown                  byte = 0x8B
	disconnectKeepAliveTimeout                    byte = 0x8D
	disconnectSessionTakenOver                    byte = 0x8E
	disconnectTopicFilterInvalid                  byte = 0x8F
	disconnectTopicNameInvalid                    byte = 0x90
	disconnectReceiveMaximumExceeded              byte = 0x93
	disconnectTopicAliasInvalid                   byte = 0x94
	disconnectPacketTooLarge                      byte = 0x95
	disconnectMessageRateTooHigh                  byte = 0x96
	disconnectQuotaExceeded                       byte = 0x97
	disconnectAdministrativeAction                byte = 0x98
	disconnectPayloadFormatInvalid                byte = 0x99
	disconnectRetainNotSupported                  byte = 0x9A
	disconnectQoSNotSupported                     byte = 0x9B
	disconnectUseAnotherServer                    byte = 0x9C
	disconnectServerMoved                         byte = 0x9D
	disconnectSharedSubscriptionsNotSupported     byte = 0x9E
	disconnectConnectionRateExceeded              byte = 0x9F
	disconnectMaximumConnectTime                  byte = 0xA0
	disconnectSubscriptionIdentifiersNotSupported byte = 0xA1
	disconnectWildcardSubscriptionsNotSupported   byte = 0xA2
)

// AUTH packet reason codes
// (https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901220)
const (
	authContinueAuthentication byte = 0x18
	authReauthenticate         byte = 0x19
)
