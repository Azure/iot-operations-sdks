// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package test

// TODO: add publish tests when the session client is able to retrieve the
// publish result when a publish operation spans multiple network connections

import (
	"context"
	"strconv"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/google/uuid"
	"github.com/stretchr/testify/require"
)

const (
	faultInjectableBrokerURL string = "mqtt://localhost:1884"
	rejectConnectFault       string = "fault:rejectconnect"
	disconnectFault          string = "fault:disconnect"
	faultRequestID           string = "fault:requestid"
	delayFault               string = "fault:delay"

	serverBusy           byte = 0x89
	administrativeAction byte = 0x98
)

func TestSessionConnectionDisconnectionHandler(t *testing.T) {
	client, err := mqtt.NewSessionClient(
		faultInjectableBrokerURL,
		mqtt.WithClientID("TestSessionConnectionDisconnectionHandler"),
		mqtt.WithSessionExpiry(10*time.Second),
	)
	require.NoError(t, err)

	conn := make(ChannelCallback[*mqtt.ConnectEvent])
	disconn := make(ChannelCallback[*mqtt.DisconnectEvent])
	connDone := client.RegisterConnectEventHandler(conn.Func)
	disconnDone := client.RegisterDisconnectEventHandler(disconn.Func)
	defer connDone()
	defer disconnDone()

	require.NoError(t, client.Start())
	defer func() { require.NoError(t, client.Stop()) }()

	<-conn

	_, err = client.Publish(
		context.Background(),
		"foo",
		[]byte("foo"),
		mqtt.WithUserProperties{
			disconnectFault: strconv.Itoa(int(administrativeAction)),
			delayFault:      "1",
		},
	)
	require.NoError(t, err)

	disconnectEvent := <-disconn
	require.Equal(t, administrativeAction, *disconnectEvent.ReasonCode)
}

func TestSessionClientHandlesFailedConnackDuringConnect(t *testing.T) {
	client, err := mqtt.NewSessionClient(
		faultInjectableBrokerURL,
		mqtt.WithClientID("TestSessionClientHandlesFailedConnackDuringConnect"),
		mqtt.WithSessionExpiry(10*time.Second),
		mqtt.WithConnectPropertiesUser(map[string]string{
			rejectConnectFault: strconv.Itoa(int(serverBusy)),
			faultRequestID:     uuid.NewString(),
		}),
	)
	require.NoError(t, err)

	conn := make(ChannelCallback[*mqtt.ConnectEvent])
	connDone := client.RegisterConnectEventHandler(conn.Func)
	defer connDone()

	require.NoError(t, client.Start())
	defer func() { require.NoError(t, client.Stop()) }()

	<-conn

	// If we get here, we successfully connected despite the fault injection.
}

func TestSessionClientHandlesDisconnectDuringSubscribe(t *testing.T) {
	client, err := mqtt.NewSessionClient(
		faultInjectableBrokerURL,
		mqtt.WithClientID("TestSessionClientHandlesDisconnectDuringSubscribe"),
		mqtt.WithSessionExpiry(10*time.Second),
		mqtt.WithKeepAlive(10*time.Second),
	)
	require.NoError(t, err)

	require.NoError(t, client.Start())
	defer func() { require.NoError(t, client.Stop()) }()

	_, err = client.Subscribe(
		context.Background(),
		"test-topic",
		mqtt.WithUserProperties{
			disconnectFault: strconv.Itoa(int(administrativeAction)),
			faultRequestID:  uuid.NewString(),
		},
	)
	require.NoError(t, err)
}

func TestSessionClientHandlesDisconnectDuringUnsubscribe(t *testing.T) {
	client, err := mqtt.NewSessionClient(
		faultInjectableBrokerURL,
		mqtt.WithClientID(
			"TestSessionClientHandlesDisconnectDuringUnsubscribe",
		),
		mqtt.WithSessionExpiry(10*time.Second),
		mqtt.WithKeepAlive(10*time.Second),
	)
	require.NoError(t, err)

	require.NoError(t, client.Start())
	defer func() { require.NoError(t, client.Stop()) }()

	_, err = client.Subscribe(context.Background(), "test-topic")
	require.NoError(t, err)

	_, err = client.Unsubscribe(
		context.Background(),
		"test-topic",
		mqtt.WithUserProperties{
			disconnectFault: strconv.Itoa(int(administrativeAction)),
			faultRequestID:  uuid.NewString(),
		},
	)

	require.NoError(t, err)
}
