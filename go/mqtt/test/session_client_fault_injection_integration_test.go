// contains integration test that uses our proprietary fault injectable broker

package test

// TODO: add publish tests when the session client is able to retrieve the publish result when a publish operation spans multiple network connections

import (
	"context"
	"strconv"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	protocol "github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
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

func TestSessionClientHandlesFailedConnackDuringConnect(t *testing.T) {
	uuidInstance, err := uuid.NewV7()
	require.NoError(t, err)
	uuidString := uuidInstance.String()

	userProperties := map[string]string{
		rejectConnectFault: strconv.Itoa(
			int(connectReasonCodeServerBusy),
		),
		faultRequestID: uuidString,
	}

	client, err := mqtt.NewSessionClient(
		faultInjectableBrokerURL,
		mqtt.WithClientID("TestSessionClientHandlesFailedConnackDuringConnect"),
		mqtt.WithConnectPropertiesUser(userProperties),
	)

	clientConnectedChan := make(chan struct{})
	connectEventFunc := func(*mqtt.ConnectEvent) { close(clientConnectedChan) }
	client.RegisterConnectNotificationHandler(connectEventFunc)

	require.NoError(t, err)
	require.NoError(t, client.Start())
	defer func() { _ = client.Stop() }()

	<-clientConnectedChan

	// If we get here, we successfully connected despite the fault injection.
}

func TestSessionClientHandlesDisconnectDuringSubscribe(t *testing.T) {
	client, err := mqtt.NewSessionClient(
		faultInjectableBrokerURL,
		mqtt.WithClientID("TestSessionClientHandlesDisconnectDuringSubscribe"),
		mqtt.WithKeepAlive(10*time.Second),
	)
	require.NoError(t, err)

	require.NoError(t, client.Start())
	defer func() { _ = client.Stop() }()

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
	client, err := mqtt.NewSessionClient(
		faultInjectableBrokerURL,
		mqtt.WithClientID("TestSessionClientHandlesDisconnectDuringUnsubscribe"),
		mqtt.WithKeepAlive(10*time.Second),
	)
	require.NoError(t, err)

	require.NoError(t, client.Start())
	defer func() { _ = client.Stop() }()

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
