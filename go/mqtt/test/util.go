// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package test

import (
	"sync"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
)

const (
	clientID        string = "sandycheeks"
	topicName       string = "patrick"
	topicName2      string = "plankton"
	LWTTopicName    string = "krabs"
	LWTMessage      string = "karen"
	publishMessage  string = "squidward"
	publishMessage2 string = "squarepants"
)

// getNextConnectEvent returns a channel that gets a single connect event from
// client and cleans up the handler it registered on the client after receiving
// the event.
func getNextConnectEvent(client *mqtt.SessionClient) <-chan *mqtt.ConnectEvent {
	internalChan := make(chan *mqtt.ConnectEvent)
	var connectEventOnce sync.Once
	connectEventFunc := func(connectEvent *mqtt.ConnectEvent) {
		connectEventOnce.Do(func() {
			internalChan <- connectEvent
			close(internalChan)
		})
	}
	remove := client.RegisterConnectNotificationHandler(connectEventFunc)

	connectEventChan := make(chan *mqtt.ConnectEvent, 1)
	go func() {
		event := <-internalChan
		connectEventChan <- event
		close(connectEventChan)
		remove()
	}()

	return connectEventChan
}
