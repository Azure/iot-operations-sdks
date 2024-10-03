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
	log := slog.New(tint.NewHandler(os.Stdout, nil))
	slog.SetDefault(log)

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

	client := must(statestore.New[string, string](mqttClient, statestore.WithLogger(log)))
	done := must(client.Listen(ctx))
	defer done()

	stateStoreKey := "someKey"
	stateStoreValue := "someValue"

	set := must(client.Set(ctx, stateStoreKey, stateStoreValue))
	log.Info("SET", "key", stateStoreKey, "value", set.Value, "version", set.Version)

	get := must(client.Get(ctx, stateStoreKey))
	log.Info("GET", "key", stateStoreKey, "value", get.Value, "version", get.Version)

	del := must(client.Del(ctx, stateStoreKey))
	log.Info("DEL", "key", stateStoreKey, "value", del.Value, "version", del.Version)
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
