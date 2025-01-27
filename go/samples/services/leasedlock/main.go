// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"log/slog"
	"os"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/leasedlock"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
	"github.com/google/uuid"
	"github.com/lmittmann/tint"
)

func main() {
	ctx := context.Background()
	log := slog.New(tint.NewHandler(os.Stdout, nil))
	app := must(protocol.NewApplication())

	mqttClient := mqtt.NewSessionClient(
		mqtt.TCPConnection("localhost", 1883),
		mqtt.WithSessionExpiry(600), // 10 minutes
	)

	client := must(statestore.New[string, string](app, mqttClient))
	defer client.Close()

	check(mqttClient.Start())
	check(client.Start(ctx))

	key := "someSharedResourceKey"
	lock := leasedlock.New(client, "someLock")

	// Sample of editing using Acquire/Release directly.
	for !tryEdit(ctx, log, client, lock, key, uuid.NewString()) {
	}

	// Sample of editing using Edit utility method.
	check(lock.Edit(ctx, key, time.Minute, func(
		ctx context.Context,
		value string,
	) (string, error) {
		log.Info("edit initial value", "key", key, "value", value)
		uuid, err := uuid.NewRandom()
		if err != nil {
			return "", err
		}
		value = uuid.String()
		log.Info("edit final value", "key", key, "value", value)
		return value, nil
	}))

	get := must(client.Get(ctx, key))
	log.Info("value after edit", "key", key, "value", get.Value)
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
