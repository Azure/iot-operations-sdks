// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"github.com/eclipse/paho.golang/paho"
)

func buildConnectPacket(
	clientID string,
	connSettings *connectionSettings,
	isInitialConn bool,
) *paho.Connect {
	// Bound checks have already been performed during the connection settings
	//initialization.
	sessionExpiryInterval := uint32(connSettings.sessionExpiry.Seconds())
	properties := paho.ConnectProperties{
		SessionExpiryInterval: &sessionExpiryInterval,
		ReceiveMaximum:        &connSettings.receiveMaximum,
		// https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901053
		// We need user properties by default.
		RequestProblemInfo: true,
		User: mapToUserProperties(
			connSettings.userProperties,
		),
	}

	// LWT.
	var willMessage *paho.WillMessage
	if connSettings.willMessage != nil {
		willMessage = &paho.WillMessage{
			Retain:  connSettings.willMessage.Retain,
			QoS:     connSettings.willMessage.QoS,
			Topic:   connSettings.willMessage.Topic,
			Payload: connSettings.willMessage.Payload,
		}
	}

	var willProperties *paho.WillProperties
	if connSettings.willProperties != nil {
		willDelayInterval := uint32(
			connSettings.willProperties.WillDelayInterval.Seconds(),
		)
		messageExpiry := uint32(
			connSettings.willProperties.MessageExpiry.Seconds(),
		)

		willProperties = &paho.WillProperties{
			WillDelayInterval: &willDelayInterval,
			PayloadFormat:     &connSettings.willProperties.PayloadFormat,
			MessageExpiry:     &messageExpiry,
			ContentType:       connSettings.willProperties.ContentType,
			ResponseTopic:     connSettings.willProperties.ResponseTopic,
			CorrelationData:   connSettings.willProperties.CorrelationData,
			User: mapToUserProperties(
				connSettings.willProperties.User,
			),
		}
	}

	return &paho.Connect{
		ClientID:       clientID,
		CleanStart:     isInitialConn,
		Username:       connSettings.username,
		UsernameFlag:   connSettings.username != "",
		Password:       connSettings.password,
		PasswordFlag:   len(connSettings.password) != 0,
		KeepAlive:      uint16(connSettings.keepAlive.Seconds()),
		WillMessage:    willMessage,
		WillProperties: willProperties,
		Properties:     &properties,
	}
}
