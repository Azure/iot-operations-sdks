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
		Topic   string
		done    chan struct{}
		payload string
	}

	WaitConn struct {
		Wait chan struct{}
		net.Conn
	}
)

func (cc ChannelCallback[T]) Func(v T) {
	cc <- v
}

func (t *TestTopicHandled) Init(payload string) {
	t.done = make(chan struct{})
	t.payload = payload
}

func (t *TestTopicHandled) Wait() {
	<-t.done
}

func (t *TestTopicHandled) Func(_ context.Context, msg *mqtt.Message) {
	if mqtt.IsTopicFilterMatch(t.Topic, msg.Topic) &&
		string(msg.Payload) == t.payload {
		close(t.done)
	}
	msg.Ack()
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
