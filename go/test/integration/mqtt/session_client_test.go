// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package mqtt

import (
	"context"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/stretchr/testify/require"
)

const (
	serverHost = "localhost"
	serverPort = 1883

	topicName  = "patrick"
	topicName2 = "plankton"
	payload    = "squidward"
	payload2   = "squarepants"
)

func TestConnect(t *testing.T) {
	client, err := mqtt.NewSessionClient(
		mqtt.RandomClientID(),
		mqtt.TCPConnection(serverHost, serverPort),
	)
	require.NoError(t, err)

	conn := make(ChannelCallback[*mqtt.ConnectEvent])
	connDone := client.RegisterConnectEventHandler(conn.Func)
	defer connDone()

	require.NoError(t, client.Start())
	<-conn
	require.NoError(t, client.Stop())
}

func TestDisconnectWithoutConnect(t *testing.T) {
	client, err := mqtt.NewSessionClient(
		mqtt.RandomClientID(),
		mqtt.TCPConnection(serverHost, serverPort),
	)
	require.NoError(t, err)

	require.Error(t, client.Stop())
}

func TestSubscribePublishUnsubscribe(t *testing.T) {
	ctx := context.Background()

	client, err := mqtt.NewSessionClient(
		mqtt.RandomClientID(),
		mqtt.TCPConnection(serverHost, serverPort),
	)
	require.NoError(t, err)

	require.NoError(t, client.Start())
	defer func() { require.NoError(t, client.Stop()) }()

	executed := make(chan struct{})
	done := client.RegisterMessageHandler(
		func(_ context.Context, msg *mqtt.Message) {
			require.Equal(t, topicName, msg.Topic)
			require.Equal(t, []byte(payload), msg.Payload)

			close(executed)
		},
	)
	defer done()

	_, err = client.Subscribe(ctx, topicName)
	require.NoError(t, err)

	_, err = client.Publish(ctx, topicName, []byte(payload))
	require.NoError(t, err)

	<-executed

	_, err = client.Unsubscribe(ctx, topicName)
	require.NoError(t, err)
}

// This test may take 4-5 seconds as it involves a connection interruption.
func TestRequestQueue(t *testing.T) {
	ctx := context.Background()

	// Allow the initial connection.
	conn := WaitConn{Wait: make(chan struct{}, 1)}
	conn.Wait <- struct{}{}

	client, err := mqtt.NewSessionClient(mqtt.RandomClientID(), conn.Provider)
	require.NoError(t, err)

	require.NoError(t, client.Start())
	defer func() { require.NoError(t, client.Stop()) }()

	disconn := make(ChannelCallback[*mqtt.DisconnectEvent])
	test1 := TestTopicHandled{Topic: topicName}
	test2 := TestTopicHandled{Topic: topicName2}
	disconnDone := client.RegisterDisconnectEventHandler(disconn.Func)
	done1 := client.RegisterMessageHandler(test1.Func)
	done2 := client.RegisterMessageHandler(test2.Func)
	defer disconnDone()
	defer done1()
	defer done2()

	// Use QoS 1 for these tests to verify ack.
	qos := mqtt.WithQoS(1)

	// Operations tested with a good connection.
	test1.Init(payload)

	_, err = client.Subscribe(ctx, topicName, qos)
	require.NoError(t, err)

	_, err = client.Publish(ctx, test1.Topic, []byte(payload), qos)
	require.NoError(t, err)

	test1.Wait()

	// Close the connection and wait for it to register.
	require.NoError(t, conn.Close())
	<-disconn

	// Note: There's a good chance this disconnection happens before the ack is
	// successfully sent to the broker, which means the initial message might
	// get replayed when we reconnect. Because of that, we use (and test) a
	// different payload for the follow-up messages.

	test1.Init(payload2)
	go func() {
		_, err := client.Publish(ctx, test1.Topic, []byte(payload2), qos)
		require.NoError(t, err)
	}()

	test2.Init(payload2)
	go func() {
		_, err = client.Subscribe(ctx, test2.Topic, qos)
		require.NoError(t, err)

		_, err := client.Publish(ctx, test2.Topic, []byte(payload2), qos)
		require.NoError(t, err)
	}()

	// Give the goroutines time to run and block.
	time.Sleep(time.Second)

	// Open up the reconnection.
	conn.Wait <- struct{}{}

	test1.Wait()
	test2.Wait()
}
