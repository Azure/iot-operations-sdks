// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import (
	"context"
	"log/slog"
	"reflect"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/eclipse/paho.golang/paho"
	"github.com/iancoleman/strcase"
)

type Logger struct{ log.Logger }

func (l Logger) PacketLog(
	ctx context.Context,
	level slog.Level,
	msg string,
	attrs ...slog.Attr,
) {
	// We're logging a message at possibly a non-debug level, but packet
	// information is only logged at a debug level, so don't spam messages
	// when the context is missing.
	if !l.Enabled(ctx, slog.LevelDebug) {
		return
	}

	l.Log(ctx, level, msg, attrs...)
}

func (l Logger) Packet(ctx context.Context, name string, packet any) {
	// This is expensive; bail out if we don't need it.
	if !l.Enabled(ctx, slog.LevelDebug) {
		return
	}

	val := realValue(reflect.ValueOf(packet))
	l.Log(ctx, slog.LevelDebug, name, reflectAttrs(val)...)
}

func reflectAttrs(val reflect.Value) []slog.Attr {
	typ := val.Type()
	num := typ.NumField()
	var attrs []slog.Attr
	for i := range num {
		f := typ.Field(i)
		if !f.IsExported() {
			continue
		}

		attrs = append(attrs, reflectAttr(
			strcase.ToSnake(f.Name),
			realValue(val.Field(i)),
		)...)
	}
	return attrs
}

func reflectAttr(name string, val reflect.Value) []slog.Attr {
	// Ignore zero values to keep the log cleaner.
	if val.Kind() == reflect.Invalid || val.IsZero() {
		return nil
	}

	switch name {
	// Paho's struct nesting is not particularly useful to log.
	case "properties":
		return reflectAttrs(val)

	// Subscriptions are one-at-a-time for the session client.
	case "subscriptions":
		if subs, ok := val.Interface().([]paho.SubscribeOptions); ok {
			return reflectAttrs(reflect.ValueOf(subs[0]))
		}
	case "topics":
		if topics, ok := val.Interface().([]string); ok {
			return []slog.Attr{slog.String("topic", topics[0])}
		}
	case "reasons":
		if reasons, ok := val.Interface().([]byte); ok {
			return []slog.Attr{slog.Int("reason_code", int(reasons[0]))}
		}

	// Fix QoS not being actually PascalCased.
	case "qo_s":
		return []slog.Attr{slog.Any("qos", val.Interface())}
	}

	switch v := val.Interface().(type) {
	case []byte:
		return []slog.Attr{slog.String(name, string(v))}

	case paho.UserProperties:
		if len(v) == 0 {
			return nil
		}
		attrs := make([]any, len(v))
		for i, p := range v {
			attrs[i] = slog.String(p.Key, p.Value)
		}
		return []slog.Attr{slog.Group(name, attrs...)}
	}

	if val.Kind() == reflect.Struct {
		as := reflectAttrs(val)
		if len(as) == 0 {
			return nil
		}

		cpy := make([]any, len(as))
		for i, a := range as {
			cpy[i] = a
		}
		return []slog.Attr{slog.Group(name, cpy...)}
	}

	return []slog.Attr{slog.Any(name, val.Interface())}
}

func realValue(typ reflect.Value) reflect.Value {
	for typ.Kind() == reflect.Pointer {
		typ = typ.Elem()
	}
	return typ
}
