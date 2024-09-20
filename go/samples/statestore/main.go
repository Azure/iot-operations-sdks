package main

import (
	"context"
	"fmt"
	"log/slog"
	"os"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
	"github.com/lmittmann/tint"
)

func main() {
	ctx := context.Background()
	slog.SetDefault(slog.New(tint.NewHandler(os.Stdout, nil)))

	clientID := fmt.Sprintf("sampleClient-%d", time.Now().UnixMilli())
	connStr := fmt.Sprintf(
		"ClientID=%s;HostName=%s;TcpPort=%s;SessionExpiry=%s",
		clientID,
		"localhost",
		"1883",
		"PT10M",
	)
	mqttClient := must1(mqtt.NewSessionClientFromConnectionString(connStr))
	check(mqttClient.Connect(ctx))

	client := must1(statestore.New(mqttClient))
	done := must1(client.Listen(ctx))
	defer done()

	stateStoreKey := "someKey"
	stateStoreValue := "someValue"

	set, ts := must2(client.Set(ctx, stateStoreKey, []byte(stateStoreValue)))
	slog.Info("SET", "key", stateStoreKey, "value", set, "ts", ts)

	data, ts := must2(client.Get(ctx, stateStoreKey))
	slog.Info("GET", "key", stateStoreKey, "value", string(data), "ts", ts)

	del, ts := must2(client.Del(ctx, stateStoreKey))
	slog.Info("DEL", "key", stateStoreKey, "value", del, "ts", ts)
}

func check(e error) {
	if e != nil {
		panic(e)
	}
}

func must1[T any](t T, e error) T {
	check(e)
	return t
}

func must2[T any, U any](t T, u U, e error) (T, U) {
	check(e)
	return t, u
}
