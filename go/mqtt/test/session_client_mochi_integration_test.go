// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package test

import (
	"context"
	"fmt"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	mochi "github.com/mochi-mqtt/server/v2"
	"github.com/mochi-mqtt/server/v2/hooks/auth"
	"github.com/mochi-mqtt/server/v2/listeners"
	"github.com/stretchr/testify/require"
)

const (
	mochiTCPPort  int    = 1234
	mochiUserName string = "gary"
	mochiPassword string = "pineapple"
)

func createSessionClientOnMochi() (*mqtt.SessionClient, error) {
	return mqtt.NewSessionClientFromConnectionString(
		fmt.Sprintf("HostName=localhost;TcpPort=%d;Username=%s;Password=%s",
			mochiTCPPort,
			mochiUserName,
			mochiPassword,
		),
	)
}

func TestWithMochi(t *testing.T) {
	ledger := &auth.Ledger{
		// Auth disallows all by default
		Auth: auth.AuthRules{
			{
				Username: auth.RString(mochiUserName),
				Password: auth.RString(mochiPassword),
				Allow:    true,
			},
		},
	}

	server := mochi.New(nil)
	err := server.AddHook(
		new(auth.Hook),
		&auth.Options{
			Ledger: ledger,
		},
	)
	require.NoError(t, err)

	cfg := listeners.NewTCP(listeners.Config{
		Type:    "tcp",
		Address: fmt.Sprintf("localhost:%d", mochiTCPPort),
	})
	require.NoError(t, server.AddListener(cfg))

	require.NoError(t, server.Serve())

	t.Cleanup(func() { server.Close() })

	t.Run("TestConnect", func(t *testing.T) {
		client, err := createSessionClientOnMochi()
		require.NoError(t, err)
		require.NoError(t, client.Connect(context.Background()))
		t.Cleanup(func() { _ = client.Disconnect() })
	})

	t.Run("TestSubscribeUnsubscribe", func(t *testing.T) {
		client, err := createSessionClientOnMochi()
		require.NoError(t, err)
		require.NoError(t, client.Connect(context.Background()))
		t.Cleanup(func() { _ = client.Disconnect() })

		sub, err := client.Subscribe(
			context.Background(),
			topicName,
			func(context.Context, *mqtt.Message) error {
				return nil
			},
		)
		require.NoError(t, err)

		require.NoError(t, sub.Unsubscribe(context.Background()))
	})

	t.Run("TestSubscribePublish", func(t *testing.T) {
		client, err := createSessionClientOnMochi()
		require.NoError(t, err)
		require.NoError(t, client.Connect(context.Background()))
		t.Cleanup(func() { _ = client.Disconnect() })

		subscribed := make(chan struct{})
		_, err = client.Subscribe(
			context.Background(),
			topicName,
			func(_ context.Context, msg *mqtt.Message) error {
				require.Equal(t, topicName, msg.Topic)
				require.Equal(t, []byte(publishMessage), msg.Payload)
				close(subscribed)
				return nil
			},
		)
		require.NoError(t, err)

		err = client.Publish(
			context.Background(),
			topicName,
			[]byte(publishMessage),
		)

		require.NoError(t, err)

		<-subscribed
	})
}
