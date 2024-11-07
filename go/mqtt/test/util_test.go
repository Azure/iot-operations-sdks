// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package test

import (
	"context"

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

func noopHandler(context.Context, *mqtt.Message) bool { return true }

type ChannelCallback[T any] chan T

func (cc ChannelCallback[T]) Func(v T) {
	cc <- v
}
