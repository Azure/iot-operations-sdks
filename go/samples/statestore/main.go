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
	mqttClient := must(mqtt.NewSessionClientFromConnectionString(connStr))
	check(mqttClient.Connect(ctx))

	client := must(statestore.New(mqttClient))
	done := must(client.Listen(ctx))
	defer done()

	stateStoreKey := "someKey"
	stateStoreValue := "someValue"

	set := must(client.Set(ctx, stateStoreKey, []byte(stateStoreValue)))
	slog.Info("SET", "key", stateStoreKey, "value", set.Value, "ts", set.Timestamp)

	get := must(client.Get(ctx, stateStoreKey))
	slog.Info("GET", "key", stateStoreKey, "value", string(get.Value), "ts", get.Timestamp)

	del := must(client.Del(ctx, stateStoreKey))
	slog.Info("DEL", "key", stateStoreKey, "value", del.Value, "ts", del.Timestamp)
}

func check(e error) {
	if e != nil {
		panic(e)
	}
}

func must[T any](t T, e error) T {
	check(e)
	return t
}
