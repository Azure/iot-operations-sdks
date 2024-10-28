package main

import (
	"context"
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

	mqttClient := must(mqtt.NewSessionClient(
		"tcp://localhost:1883",
		mqtt.WithSessionExpiry(10*time.Minute),
	))

	stateStoreKey := "someKey"
	stateStoreValue := "someValue"

	client := must(statestore.New[string, string](mqttClient, statestore.WithLogger(log)))
	defer client.Close()

	kn, rm := client.Notify(stateStoreKey)
	defer rm()

	check(mqttClient.Start())
	check(client.Start(ctx))

	check(client.KeyNotify(ctx, stateStoreKey))
	defer func() { check(client.KeyNotifyStop(ctx, stateStoreKey)) }()

	must(client.Set(ctx, stateStoreKey, stateStoreValue))
	n := <-kn
	log.Info(n.Operation, "key", n.Key, "value", n.Value)

	get := must(client.Get(ctx, stateStoreKey))
	log.Info("GET", "key", stateStoreKey, "value", get.Value, "version", get.Version)

	must(client.Del(ctx, stateStoreKey))
	n = <-kn
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
