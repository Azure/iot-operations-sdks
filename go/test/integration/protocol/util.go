// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"testing"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/stretchr/testify/require"
)

func sessionClients(
	t *testing.T,
) (client, server *mqtt.SessionClient, done func()) {
	var err error

	conn := mqtt.TCPConnection("localhost", 1883)

	client, err = mqtt.NewSessionClient(conn)
	require.NoError(t, err)
	require.NoError(t, client.Start())

	server, err = mqtt.NewSessionClient(conn)
	require.NoError(t, err)
	require.NoError(t, server.Start())

	return client, server, func() {
		require.NoError(t, client.Stop())
		require.NoError(t, server.Stop())
	}
}
