// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package test

import (
	"context"
	"os/exec"
	"strings"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/stretchr/testify/require"
)

const (
	mqServerHostname string = "localhost"
	mqServerPort     int    = 1883
	mqServerPod      string = "aio-broker-frontend-0"
)

func shouldRunIntegrationTest(t *testing.T) {
	// check MQ pod status
	cmd := exec.Command(
		"kubectl",
		"get",
		"pod",
		mqServerPod,
		"-o",
		"jsonpath={.status.phase}",
	)

	output, err := cmd.Output()
	if err != nil {
		t.Fatalf("can't get %s status", mqServerPod)
	}

	status := strings.TrimSpace(string(output))
	if status != "Running" {
		t.Fatalf("%s is not running", mqServerPod)
	}
}

func TestConnectMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	client, err := mqtt.NewSessionClient(
		mqtt.TCPConnection(
			faultInjectableBrokerHostname,
			faultInjectableBrokerPort,
		),
	)
	require.NoError(t, err)

	require.NoError(t, client.Start())
	require.NoError(t, client.Stop())
}

func TestConnectWithTimeoutMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	client, err := mqtt.NewSessionClient(
		mqtt.TCPConnection(
			faultInjectableBrokerHostname,
			faultInjectableBrokerPort,
		),
	)
	require.NoError(t, err)

	// ctx would be canceled after a successful initial connection,
	// ensuring the connection remains active.
	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)

	require.NoError(t, client.Start())

	done := client.RegisterMessageHandler(noopHandler)
	defer done()

	_, err = client.Subscribe(ctx, topicName)
	require.NoError(t, err)

	cancel()

	// connection is still active so we can still subscribe.
	ctx2 := context.Background()
	_, err = client.Subscribe(ctx2, topicName2)
	require.NoError(t, err)
	require.NoError(t, client.Stop())
}

func TestDisconnectWithoutConnectMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	client, err := mqtt.NewSessionClient(
		mqtt.TCPConnection(
			faultInjectableBrokerHostname,
			faultInjectableBrokerPort,
		),
	)
	require.NoError(t, err)
	require.Error(t, client.Stop())
}

func TestSubscribeUnsubscribeMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	client, err := mqtt.NewSessionClient(
		mqtt.TCPConnection(
			faultInjectableBrokerHostname,
			faultInjectableBrokerPort,
		),
	)
	require.NoError(t, err)

	ctx := context.Background()
	require.NoError(t, client.Start())

	done := client.RegisterMessageHandler(noopHandler)
	defer done()

	_, err = client.Subscribe(ctx, topicName)
	require.NoError(t, err)

	_, err = client.Unsubscribe(ctx, topicName)
	require.NoError(t, err)
}

// This test may take 4-5 seconds as it involves a connection interruption.
func TestRequestQueueMQ(t *testing.T) {
	// TODO: revisit this skipped test
	t.Skip(
		"Skipping this test due to potential race condition with pod deletion",
	)
	shouldRunIntegrationTest(t)

	client, err := mqtt.NewSessionClient(
		mqtt.TCPConnection(
			faultInjectableBrokerHostname,
			faultInjectableBrokerPort,
		),
		mqtt.WithSessionExpiryInterval(30),
	)
	require.NoError(t, err)

	ctx := context.Background()
	require.NoError(t, client.Start())

	executed := make(chan struct{})
	done := client.RegisterMessageHandler(
		func(_ context.Context, msg *mqtt.Message) bool {
			if msg.Topic == topicName {
				close(executed)
				return true
			}
			return false
		},
	)
	defer done()

	// Operations tested with a good connection.
	_, err = client.Subscribe(ctx, topicName)
	require.NoError(t, err)

	_, err = client.Publish(
		ctx,
		topicName,
		[]byte(publishMessage),
	)
	require.NoError(t, err)

	<-executed

	// Delete MQ pod to stop the connection for seconds.
	cmd := exec.Command(
		"kubectl",
		"delete",
		"pod",
		mqServerPod,
	)
	_, err = cmd.Output()
	require.NoError(t, err)

	// Wait for a short while so at least one operation would be queued.
	time.Sleep(100 * time.Millisecond)

	executed = make(chan struct{})
	executed2 := make(chan struct{})

	// Operations are blocking so we put them into goroutines.
	go func() {
		_, err := client.Publish(
			ctx,
			topicName,
			[]byte(publishMessage),
		)
		require.NoError(t, err)
	}()

	go func(ch chan struct{}) {
		ch2 := make(chan struct{})
		done := client.RegisterMessageHandler(
			func(_ context.Context, msg *mqtt.Message) bool {
				if msg.Topic == topicName2 {
					close(ch2)
					close(ch)
					return true
				}
				return false
			},
		)
		defer done()

		_, err = client.Subscribe(ctx, topicName2)
		require.NoError(t, err)

		_, err = client.Publish(
			ctx,
			topicName2,
			[]byte(publishMessage2),
		)
		require.NoError(t, err)
		<-ch2
	}(executed2)

	<-executed2
	<-executed

	require.NoError(t, client.Stop())
}

func TestPublishMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	client, err := mqtt.NewSessionClient(
		mqtt.TCPConnection(
			faultInjectableBrokerHostname,
			faultInjectableBrokerPort,
		),
	)
	require.NoError(t, err)

	ctx := context.Background()
	require.NoError(t, client.Start())

	executed := make(chan struct{})

	done := client.RegisterMessageHandler(
		func(_ context.Context, msg *mqtt.Message) bool {
			require.Equal(t, topicName, msg.Topic)
			require.Equal(t, []byte(publishMessage), msg.Payload)

			close(executed)
			return true
		},
	)
	defer done()

	_, err = client.Subscribe(ctx, topicName)
	require.NoError(t, err)

	_, err = client.Publish(
		ctx,
		topicName,
		[]byte(publishMessage),
	)
	require.NoError(t, err)

	<-executed

	require.NoError(t, client.Stop())
}
