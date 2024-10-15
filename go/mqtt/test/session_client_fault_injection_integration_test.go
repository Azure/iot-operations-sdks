// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package test

// TODO: add publish tests when the session client is able to retrieve the publish result when a publish operation spans multiple network connections

import (
	"context"
	"fmt"
	"strconv"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/google/uuid"
	"github.com/stretchr/testify/require"
)

const (
	faultInjectableBrokerURL                 string = "mqtt://localhost:1884"
	rejectConnectFault                       string = "fault:rejectconnect"
	disconnectFault                          string = "fault:disconnect"
	faultRequestID                           string = "fault:requestid"
	delayFault                               string = "fault:delay"
	connectReasonCodeServerBusy              byte   = 0x89
	disconnectReasonCodeAdministrativeAction byte   = 0x98
)

func TestSessionConnectionDisconnectionHandler(t *testing.T) {
	client, err := mqtt.NewSessionClient(
		faultInjectableBrokerURL,
		mqtt.WithClientID("TestSessionConnectionDisconnectionHandler"),
	)

	clientConnectedChan := make(chan struct{})
	clientDisconnectedChan := make(chan *mqtt.DisconnectEvent)
	connectEventFunc := func(*mqtt.ConnectEvent) { close(clientConnectedChan) }
	disconnectEventFunc := func(disconnect *mqtt.DisconnectEvent) {
		clientDisconnectedChan <- disconnect
	}
	removeConnectEventFunc := client.RegisterConnectNotificationHandler(connectEventFunc)
	removeDisconnectEventFunc := client.RegisterDisconnectNotificationHandler(disconnectEventFunc)
	defer removeConnectEventFunc()
	defer removeDisconnectEventFunc()

	require.NoError(t, err)
	require.NoError(t, client.Start())
	defer func() { _ = client.Stop() }()

	<-clientConnectedChan

	fmt.Println(strconv.Itoa(int(disconnectReasonCodeAdministrativeAction)))

	err = client.Publish(
		context.Background(),
		"foo",
		[]byte("foo"),
		mqtt.WithUserProperties{
			disconnectFault: strconv.Itoa(int(disconnectReasonCodeAdministrativeAction)),
			delayFault:      "1",
		},
	)
	require.NoError(t, err)

	disconnectEvent := <-clientDisconnectedChan
	require.Equal(t, disconnectReasonCodeAdministrativeAction, disconnectEvent.DisconnectPacket.ReasonCode)
}

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

	err = client.Subscribe(
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
	client, err := mqtt.NewSessionClient(
		faultInjectableBrokerURL,
		mqtt.WithClientID("TestSessionClientHandlesDisconnectDuringUnsubscribe"),
		mqtt.WithKeepAlive(10*time.Second),
	)
	require.NoError(t, err)

	require.NoError(t, client.Start())
	defer func() { _ = client.Stop() }()

	err = client.Subscribe(context.Background(), "test-topic")
	require.NoError(t, err)

	uuidInstance, err := uuid.NewV7()
	require.NoError(t, err)
	uuidString := uuidInstance.String()

	err = client.Unsubscribe(
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
