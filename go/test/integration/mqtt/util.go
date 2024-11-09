// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"net"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
)

type (
	ChannelCallback[T any] chan T

	TestTopicHandled struct {
		Topic string
		done  chan struct{}
	}

	WaitConn struct {
		Wait chan struct{}
		net.Conn
	}
)

func (cc ChannelCallback[T]) Func(v T) {
	cc <- v
}

func (t *TestTopicHandled) Init() {
	t.done = make(chan struct{})
}

func (t *TestTopicHandled) Wait() {
	<-t.done
}

func (t *TestTopicHandled) Func(_ context.Context, msg *mqtt.Message) bool {
	if mqtt.IsTopicFilterMatch(t.Topic, msg.Topic) {
		close(t.done)
		return true
	}
	return false
}

func (wc *WaitConn) Provider(ctx context.Context) (net.Conn, error) {
	var d net.Dialer
	var err error
	<-wc.Wait
	wc.Conn, err = d.DialContext(ctx, "tcp", "localhost:1883")
	if err != nil {
		return nil, err
	}
	return wc.Conn, nil
}
