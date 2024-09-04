// contains integration test that uses our proprietary fault injectable broker

package test

import (
	"context"
	"strconv"
	"testing"

	"github.com/google/uuid"
	"github.com/microsoft/mqtt-patterns/lib/go/mqtt"
	protocol "github.com/microsoft/mqtt-patterns/lib/go/protocol/mqtt"
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
	require.NoError(t, client.Connect(context.Background()))
	_ = client.Disconnect()
}

func TestSessionClientHandlesDisconnectDuringSubscribe(t *testing.T) {
	t.Skip(
		"session client currently fails this test with error message 'MQTT subscribe timed out'",
	)
	client, err := mqtt.NewSessionClient(faultInjectableBrokerURL)
	require.NoError(t, err)

	require.NoError(t, client.Connect(context.Background()))
	defer func() { _ = client.Disconnect() }()

	uuidInstance, err := uuid.NewV7()
	require.NoError(t, err)
	uuidString := uuidInstance.String()

	_, err = client.Subscribe(
		context.Background(),
		"test-topic",
		func(context.Context, *protocol.Message) error { return nil },
		protocol.WithUserProperties{
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

	require.NoError(t, client.Connect(context.Background()))
	defer func() { _ = client.Disconnect() }()

	subscription, err := client.Subscribe(
		context.Background(),
		"test-topic",
		func(context.Context, *protocol.Message) error { return nil },
	)
	require.NoError(t, err)

	uuidInstance, err := uuid.NewV7()
	require.NoError(t, err)
	uuidString := uuidInstance.String()

	err = subscription.Unsubscribe(
		context.Background(),
		protocol.WithUserProperties{
			disconnectFault: strconv.Itoa(
				int(disconnectReasonCodeAdministrativeAction),
			),
			faultRequestID: uuidString,
		},
	)
	require.NoError(t, err)
}
