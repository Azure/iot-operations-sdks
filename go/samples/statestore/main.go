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

	clientID := fmt.Sprintf("sampleClient-%d", time.Now().UnixMilli())
	connStr := fmt.Sprintf(
		"ClientID=%s;HostName=%s;TcpPort=%s;SessionExpiry=%s",
		clientID,
		"localhost",
		"1883",
		"PT10M",
	)
	mqttClient := must(mqtt.NewSessionClientFromConnectionString(connStr))
	client := must(statestore.New[string, string](mqttClient, statestore.WithLogger(log)))

	check(mqttClient.Connect(ctx))
	done := must(client.Listen(ctx))
	defer done()

	stateStoreKey := "someKey"
	stateStoreValue := "someValue"

	kn := must(client.KeyNotify(ctx, stateStoreKey))
	defer func() { check(kn.Stop(ctx)) }()

	must(client.Set(ctx, stateStoreKey, stateStoreValue))
	n := <-kn.C()
	log.Info(n.Operation, "key", n.Key, "value", n.Value)

	get := must(client.Get(ctx, stateStoreKey))
	log.Info("GET", "key", stateStoreKey, "value", get.Value, "version", get.Version)

	must(client.Del(ctx, stateStoreKey))
	n = <-kn.C()
	log.Info(n.Operation, "key", n.Key, "value", n.Value)
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
