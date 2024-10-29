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
	// TODO: Update connection options such as username, password
	// during connection up.

	// Bound checks have already been performed
	// during the connection settings initialization.
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

	// Enhanced Authentication.
	if connSettings.authOptions.AuthMethod != "" {
		properties.AuthMethod = connSettings.authOptions.AuthMethod
		properties.AuthData = connSettings.authOptions.AuthData
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

	// Only apply user setting for initial connection.
	cleanStart := connSettings.cleanStart
	if !isInitialConn {
		cleanStart = false
	}
	return &paho.Connect{
		ClientID:       clientID,
		CleanStart:     cleanStart,
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

func buildDisconnectPacket(
	reasonCode reasonCode,
	reasonString string,
) *paho.Disconnect {
	endSession := uint32(0)
	return &paho.Disconnect{
		ReasonCode: byte(reasonCode),
		Properties: &paho.DisconnectProperties{
			// Informs the server that the session is complete
			// and can be safely deleted on the server's end.
			SessionExpiryInterval: &endSession,
			ReasonString:          reasonString,
		},
	}
}

// packetType gets the string name of a paho packet.
func packetType(packet any) string {
	switch packet.(type) {
	case *paho.Subscribe:
		return subscribePacket
	case *paho.Unsubscribe:
		return unsubscribePacket
	case *paho.Publish:
		return publishPacket
	default:
		return "unknown packet"
	}
}

// handleError will pass errors from executed operations to error channel.
func (qp *queuedPacket) handleError(err error) {
	if err != nil {
		qp.errC <- err
	} else {
		// No errors returned; close the channel to indicate exit.
		close(qp.errC)
	}
}
