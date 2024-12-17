// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

var fatalConnackReasonCodes = map[byte]struct{}{
	connackMalformedPacket:             {},
	connackProtocolError:               {},
	connackImplementationSpecificError: {},
	connackUnsupportedProtocolVersion:  {},
	connackClientIdentifierNotValid:    {},
	connackBadUserNameOrPassword:       {},
	connackNotAuthorized:               {},
	connackBanned:                      {},
	connackBadAuthenticationMethod:     {},
	connackTopicNameInvalid:            {},
	connackPacketTooLarge:              {},
	connackPayloadFormatInvalid:        {},
	connackRetainNotSupported:          {},
	connackQoSNotSupported:             {},
	connackUseAnotherServer:            {},
	connackServerMoved:                 {},
}

// isFatalConnackReasonCode checks if the reason code in the CONNACK received
// from the server is fatal.
func isFatalConnackReasonCode(reasonCode byte) bool {
	_, ok := fatalConnackReasonCodes[reasonCode]
	return ok
}

var fatalDisconnectReasonCodes = map[byte]struct{}{
	disconnectMalformedPacket:                     {},
	disconnectProtocolError:                       {},
	disconnectNotAuthorized:                       {},
	disconnectSessionTakenOver:                    {},
	disconnectTopicFilterInvalid:                  {},
	disconnectTopicNameInvalid:                    {},
	disconnectTopicAliasInvalid:                   {},
	disconnectPacketTooLarge:                      {},
	disconnectPayloadFormatInvalid:                {},
	disconnectRetainNotSupported:                  {},
	disconnectQoSNotSupported:                     {},
	disconnectServerMoved:                         {},
	disconnectSharedSubscriptionsNotSupported:     {},
	disconnectSubscriptionIdentifiersNotSupported: {},
	disconnectWildcardSubscriptionsNotSupported:   {},
}

// isFatalDisconnectReasonCode checks if the reason code in the DISCONNECT
// received from the server is fatal.
func isFatalDisconnectReasonCode(reasonCode byte) bool {
	_, ok := fatalDisconnectReasonCodes[reasonCode]
	return ok
}
