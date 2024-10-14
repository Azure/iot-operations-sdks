// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package test

import (
	"context"
	"net"
	"os"
	"os/exec"
	"strings"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/eclipse/paho.golang/paho"
	"github.com/stretchr/testify/require"
)

const (
	mqServerURL string = "mqtt://localhost:1883"
	mqServerPod string = "aio-broker-frontend-0"
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

	client, err := mqtt.NewSessionClient(mqServerURL)
	require.NoError(t, err)

	ctx := context.Background()
	err = client.Connect(ctx)
	require.NoError(t, err)

	err = client.Disconnect()
	require.NoError(t, err)
}

func TestConnectWithTimeoutMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	client, err := mqtt.NewSessionClient(mqServerURL)
	require.NoError(t, err)

	// ctx would be canceled after a successful initial connection,
	// ensuring the connection remains active.
	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)

	err = client.Connect(ctx)
	require.NoError(t, err)

	_, err = client.Subscribe(
		ctx,
		topicName,
		func(context.Context, *mqtt.Message) error {
			return nil
		},
	)
	require.NoError(t, err)

	cancel()

	// connection is still active so we can still subscribe.
	ctx2 := context.Background()
	_, err = client.Subscribe(
		ctx2,
		topicName2,
		func(context.Context, *mqtt.Message) error {
			return nil
		},
	)
	require.NoError(t, err)

	err = client.Disconnect()
	require.NoError(t, err)
}

func TestDisconnectWithoutConnectMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	client, err := mqtt.NewSessionClient(mqServerURL)
	require.NoError(t, err)

	err = client.Disconnect()
	require.Error(t, err)
}

func TestConnectFromEnvMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	envVars := map[string]string{
		"MQTT_HOST_NAME": "localhost",
		"MQTT_TCP_PORT":  "1883",
	}

	// Set environment variables
	for key, value := range envVars {
		os.Setenv(key, value)
	}
	// Unset environment variables after the test
	defer func() {
		for key := range envVars {
			os.Unsetenv(key)
		}
	}()

	client, err := mqtt.NewSessionClientFromEnv()
	require.NoError(t, err)

	ctx := context.Background()
	err = client.Connect(ctx)
	require.NoError(t, err)
}

func TestConnectFromConnectionStringMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	connStr := "ClientId=IntegrationTestClient;" +
		"HostName=localhost;" +
		"TcpPort=1883"

	client, err := mqtt.NewSessionClientFromConnectionString(connStr)
	require.NoError(t, err)

	ctx := context.Background()
	err = client.Connect(ctx)
	require.NoError(t, err)
}

func TestSubscribeUnsubscribeMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	client, err := mqtt.NewSessionClient(mqServerURL)
	require.NoError(t, err)

	ctx := context.Background()
	err = client.Connect(ctx)
	require.NoError(t, err)

	sub, err := client.Subscribe(
		ctx,
		topicName,
		func(context.Context, *mqtt.Message) error {
			return nil
		},
	)
	require.NoError(t, err)

	err = sub.Unsubscribe(ctx)
	require.NoError(t, err)
}

// This test may take 4-5 seconds as it involves a connection interruption.
func TestRequestQueueMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	client, err := mqtt.NewSessionClient(mqServerURL)
	require.NoError(t, err)

	ctx := context.Background()
	err = client.Connect(ctx)
	require.NoError(t, err)

	// Operations tested with a good connection.
	executed := make(chan struct{})
	_, err = client.Subscribe(
		ctx,
		topicName,
		func(_ context.Context, _ *mqtt.Message) error {
			close(executed)
			return nil
		},
	)
	require.NoError(t, err)

	err = client.Publish(
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
	go func(ch chan struct{}) {
		err := client.Publish(
			ctx,
			topicName,
			[]byte(publishMessage),
		)
		require.NoError(t, err)
		close(ch)
	}(executed)

	go func(ch chan struct{}) {
		ch2 := make(chan struct{})
		_, err := client.Subscribe(
			ctx,
			topicName2,
			func(_ context.Context, _ *mqtt.Message) error {
				close(ch2)
				close(ch)
				return nil
			},
		)
		require.NoError(t, err)

		err = client.Publish(
			ctx,
			topicName2,
			[]byte(publishMessage2),
		)
		require.NoError(t, err)
		<-ch2
	}(executed2)

	<-executed2
	<-executed

	err = client.Disconnect()
	require.NoError(t, err)
}

func TestPublishMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	client, err := mqtt.NewSessionClient(mqServerURL)
	require.NoError(t, err)

	ctx := context.Background()
	err = client.Connect(ctx)
	require.NoError(t, err)

	executed := make(chan struct{})

	_, err = client.Subscribe(
		ctx,
		topicName,
		func(_ context.Context, msg *mqtt.Message) error {
			require.Equal(t, topicName, msg.Topic)
			require.Equal(t, []byte(publishMessage), msg.Payload)

			close(executed)
			return nil
		},
	)
	require.NoError(t, err)

	err = client.Publish(
		ctx,
		topicName,
		[]byte(publishMessage),
	)
	require.NoError(t, err)

	<-executed

	err = client.Disconnect()
	require.NoError(t, err)
}

func startSessionClientWithWillMessage(t *testing.T) (
	context.Context,
	context.CancelFunc,
	*mqtt.SessionClient,
) {
	ctx, cancel := context.WithCancel(context.Background())

	// Create network connection manually so it can be closed later.
	address := "localhost:1883"
	var d net.Dialer
	conn, err := d.DialContext(ctx, "tcp", address)
	require.NoError(t, err)

	config := &paho.ClientConfig{
		Conn:     conn,
		ClientID: clientID,
	}

	client, err := mqtt.NewSessionClient(
		mqServerURL,
		mqtt.WithPahoClientConfig(config),
		mqtt.WithWillMessageTopic(LWTTopicName),
		mqtt.WithWillMessagePayload([]byte(LWTMessage)),
	)
	require.NoError(t, err)

	// Close network connection so client connection would exit unexpectedly.
	// NOTE: We can also induce an LWT message by disconnecting with a error reason code rather than messing directly with the tcp socket.
	go func(c net.Conn) {
		time.Sleep(2 * time.Second)
		err := c.Close()
		require.NoError(t, err)
	}(conn)

	return ctx, cancel, client
}

func TestLastWillMessageMQ(t *testing.T) {
	shouldRunIntegrationTest(t)

	client1, err := mqtt.NewSessionClient(mqServerURL)
	require.NoError(t, err)

	ctx := context.Background()
	err = client1.Connect(ctx)
	require.NoError(t, err)

	subscribed := make(chan struct{})
	_, err = client1.Subscribe(
		ctx,
		LWTTopicName,
		func(_ context.Context, msg *mqtt.Message) error {
			require.Equal(t, LWTTopicName, msg.Topic)
			require.Equal(t, []byte(LWTMessage), msg.Payload)
			close(subscribed)
			return nil
		},
	)
	require.NoError(t, err)

	ctx, cancel, client2 := startSessionClientWithWillMessage(t)
	defer cancel()

	err = client2.Connect(ctx)
	require.NoError(t, err)

	<-subscribed
}
