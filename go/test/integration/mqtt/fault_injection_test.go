// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package mqtt

// TODO: add publish tests when the session client is able to retrieve the
// publish result when a publish operation spans multiple network connections

import (
	"context"
	"strconv"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/google/uuid"
	"github.com/stretchr/testify/require"
)

const (
	faultInjectableBrokerHostname = "localhost"
	faultInjectableBrokerPort     = 1884
	rejectConnectFault            = "fault:rejectconnect"
	disconnectFault               = "fault:disconnect"
	faultRequestID                = "fault:requestid"
	delayFault                    = "fault:delay"

	serverBusy           byte = 0x89
	administrativeAction byte = 0x98
)

func TestSessionClientHandlesDisconnectWhileIdle(t *testing.T) {
	client := mqtt.NewSessionClient(
		"TestSessionClientHandlesDisconnectWhileIdle",
		mqtt.TCPConnection(
			faultInjectableBrokerHostname,
			faultInjectableBrokerPort,
		),
		mqtt.WithSessionExpiry(10),
	)

	conn := make(ChannelCallback[*mqtt.ConnectEvent])
	disconn := make(ChannelCallback[*mqtt.DisconnectEvent])
	connDone := client.RegisterConnectEventHandler(conn.Func)
	disconnDone := client.RegisterDisconnectEventHandler(disconn.Func)
	defer connDone()
	defer disconnDone()

	require.NoError(t, client.Start())
	defer func() { require.NoError(t, client.Stop()) }()

	<-conn

	_, err := client.Publish(
		context.Background(),
		"test-topic",
		[]byte("test-data"),
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
	client := mqtt.NewSessionClient(
		"TestSessionClientHandlesFailedConnackDuringConnect",
		mqtt.TCPConnection(
			faultInjectableBrokerHostname,
			faultInjectableBrokerPort,
		),
		mqtt.WithSessionExpiry(10),
		mqtt.WithConnectUserProperties{
			rejectConnectFault: strconv.Itoa(int(serverBusy)),
			faultRequestID:     uuid.NewString(),
		},
	)

	conn := make(ChannelCallback[*mqtt.ConnectEvent])
	connDone := client.RegisterConnectEventHandler(conn.Func)
	defer connDone()

	require.NoError(t, client.Start())
	defer func() { require.NoError(t, client.Stop()) }()

	<-conn

	// If we get here, we successfully connected despite the fault injection.
}

func TestSessionClientHandlesDisconnectDuringSubscribe(t *testing.T) {
	client := mqtt.NewSessionClient(
		"TestSessionClientHandlesDisconnectDuringSubscribe",
		mqtt.TCPConnection(
			faultInjectableBrokerHostname,
			faultInjectableBrokerPort,
		),
		mqtt.WithSessionExpiry(10),
		mqtt.WithKeepAlive(10),
	)

	require.NoError(t, client.Start())
	defer func() { require.NoError(t, client.Stop()) }()

	_, err := client.Subscribe(
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
	client := mqtt.NewSessionClient(
		"TestSessionClientHandlesDisconnectDuringUnsubscribe",
		mqtt.TCPConnection(
			faultInjectableBrokerHostname,
			faultInjectableBrokerPort,
		),
		mqtt.WithSessionExpiry(10),
		mqtt.WithKeepAlive(10),
	)

	require.NoError(t, client.Start())
	defer func() { require.NoError(t, client.Stop()) }()

	_, err := client.Subscribe(context.Background(), "test-topic")
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
