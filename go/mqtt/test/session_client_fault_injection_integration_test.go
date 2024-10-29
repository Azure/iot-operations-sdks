// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package test

import (
	"context"
	"strconv"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/google/uuid"
	"github.com/stretchr/testify/require"
)

const (
	faultInjectableBrokerURL                 string = "mqtt://localhost:1884"
	rejectConnectFault                       string = "fault:rejectconnect"
	disconnectFault                          string = "fault:disconnect"
	faultRequestID                           string = "fault:requestid"
	connectReasonCodeServerBusy              byte   = 0x89
	disconnectReasonCodeAdministrativeAction byte   = 0x98
)

// TODO: add publish tests when the session client is able to retrieve the publish result when a publish operation spans multiple network connections

func TestSessionClientHandlesFailedConnackDuringConnect(t *testing.T) {
	uuidInstance, err := uuid.NewV7()
	require.NoError(t, err)
	uuidString := uuidInstance.String()

	userProperties := map[string]string{
		rejectConnectFault: strconv.Itoa(
			int(connectReasonCodeServerBusy),
		), // TODO: ensure base 10 representation is correct.
		faultRequestID: uuidString,
	}

	client, err := mqtt.NewSessionClient(
		faultInjectableBrokerURL,
		mqtt.WithConnectPropertiesUser(userProperties),
	)
	require.NoError(t, err)
	require.NoError(t, client.Start())
	_ = client.Stop()
}

func TestSessionClientHandlesDisconnectDuringSubscribe(t *testing.T) {
	t.Skip(
		"session client currently fails this test with error message 'MQTT subscribe timed out'",
	)
	client, err := mqtt.NewSessionClient(faultInjectableBrokerURL)
	require.NoError(t, err)

	require.NoError(t, client.Start())
	defer func() { _ = client.Stop() }()

	uuidInstance, err := uuid.NewV7()
	require.NoError(t, err)
	uuidString := uuidInstance.String()

	done := client.RegisterMessageHandler(noopHandler)
	defer done()

	_, err = client.Subscribe(
		context.Background(),
		"test-topic",
		mqtt.WithUserProperties{
			disconnectFault: strconv.Itoa(
				int(disconnectReasonCodeAdministrativeAction),
			),
			faultRequestID: uuidString,
		},
	)

	require.NoError(t, err)
}

func TestSessionClientHandlesDisconnectDuringUnsubscribe(t *testing.T) {
	t.Skip(
		"session client currently fails this test with error message 'MQTT unsubscribe timed out'",
	)

	client, err := mqtt.NewSessionClient(faultInjectableBrokerURL)
	require.NoError(t, err)

	require.NoError(t, client.Start())
	defer func() { _ = client.Stop() }()

	done := client.RegisterMessageHandler(noopHandler)
	defer done()

	_, err = client.Subscribe(context.Background(), "test-topic")
	require.NoError(t, err)

	uuidInstance, err := uuid.NewV7()
	require.NoError(t, err)
	uuidString := uuidInstance.String()

	_, err = client.Unsubscribe(
		context.Background(),
		"test-topic",
		mqtt.WithUserProperties{
			disconnectFault: strconv.Itoa(
				int(disconnectReasonCodeAdministrativeAction),
			),
			faultRequestID: uuidString,
		},
	)

	require.NoError(t, err)
}
