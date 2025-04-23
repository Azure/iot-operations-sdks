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

	mqttClient := must(mqtt.NewSessionClientFromEnv(
		mqtt.WithLogger(slog.Default()),
	))

	client := must(statestore.New[string, string](app, mqttClient))
	defer client.Close()

	check(mqttClient.Start())
	check(client.Start(ctx))

	key := "someSharedResourceKey"
	lock := leasedlock.NewLock(client, "someLock")

	// Sample of editing using Lock/Unlock directly.
	for !tryEdit(ctx, log, client, lock, key, uuid.NewString()) {
	}

	// Sample of editing using Edit utility method.
	check(lock.Edit(ctx, key, time.Minute, func(
		ctx context.Context,
		value string,
		_ bool,
	) (string, bool, error) {
		log.Info("edit initial value", "key", key, "value", value)
		uuid, err := uuid.NewRandom()
		if err != nil {
			return "", false, err
		}
		value = uuid.String()
		log.Info("edit final value", "key", key, "value", value)
		return value, true, nil
	}))

	get := must(client.Get(ctx, key))
	log.Info("value after edit", "key", key, "value", get.Value)

	// Sample of renewing lease.
	lease := leasedlock.NewLease(client, "someLease")
	if must(lease.Acquire(ctx, time.Minute, leasedlock.WithRenew(2*time.Second))) {
		defer lease.Release(ctx)

		for range 10 {
			log.Info("current token", "token", must(lease.Token(ctx)))
			time.Sleep(time.Second)
		}
	}
}

func tryEdit[K, V statestore.Bytes](
	ctx context.Context,
	log *slog.Logger,
	client *statestore.Client[K, V],
	lock leasedlock.Lock[K, V],
	key K,
	value V,
) bool {
	check(lock.Lock(ctx, time.Minute))
	log.Info("acquired lock", "name", lock.Name)
	defer lock.Unlock(ctx)

	ft := must(lock.Token(ctx))
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
