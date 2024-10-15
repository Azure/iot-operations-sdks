// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"log/slog"
	"reflect"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
)

type logger struct{ log.Logger }

func (l logger) Packet(ctx context.Context, packet any) {
	// This is expensive; bail out if we don't need it.
	if !l.Enabled(ctx, slog.LevelInfo) {
		return
	}

	val := realValue(reflect.ValueOf(packet))
	l.Log(ctx, slog.LevelInfo, val.Type().Name(), reflectAttr(val)...)
}

func reflectAttr(val reflect.Value) []slog.Attr {
	typ := val.Type()
	num := typ.NumField()
	attrs := make([]slog.Attr, 0, num)
	for i := range num {
		f := typ.Field(i)
		if !f.IsExported() {
			continue
		}

		v := realValue(val.Field(i))

		switch v.Kind() {
		case reflect.Struct:
			as := reflectAttr(v)
			cpy := make([]any, len(as))
			for i, a := range as {
				cpy[i] = a
			}
			attrs = append(attrs, slog.Group(f.Name, cpy...))

		case reflect.Invalid:
			attrs = append(attrs, slog.Any(f.Name, nil))

		default:
			attrs = append(attrs, slog.Any(f.Name, v.Interface()))
		}
	}
	return attrs
}

func realValue(typ reflect.Value) reflect.Value {
	for typ.Kind() == reflect.Pointer {
		typ = typ.Elem()
	}
	return typ
}
