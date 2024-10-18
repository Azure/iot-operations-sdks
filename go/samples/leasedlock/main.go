package main

import (
	"context"
	"fmt"
	"log/slog"
	"os"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/services/leasedlock"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
	"github.com/google/uuid"
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
	defer client.Close()

	check(mqttClient.Connect(ctx))
	check(client.Start(ctx))

	lock := leasedlock.New(client, "someLock")

	for !tryEdit(ctx, log, client, lock, "someSharedResourceKey", uuid.New().String()) {
	}
}

func tryEdit[K, V statestore.Bytes](
	ctx context.Context,
	log *slog.Logger,
	client *statestore.Client[K, V],
	lock *leasedlock.Lock[K, V],
	key K,
	value V,
) bool {
	ft := must(lock.Acquire(ctx, time.Minute))
	log.Info("acquired lock", "name", lock.Name)
	defer lock.Release(ctx)

	set := must(client.Set(ctx, key, value, statestore.WithFencingToken(ft)))
	if set.Value {
		log.Info("successfully changed value", "key", key, "value", value)
	} else {
		log.Info("failed to change value", "key", key)
	}
	return set.Value
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
