// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"log/slog"
	"net/url"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/errutil"
	"github.com/relvacode/iso8601"
)

// CloudEvent provides an implementation of the CloudEvents 1.0 spec; see:
// https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md
type CloudEvent struct {
	ID          string
	Source      *url.URL
	SpecVersion string
	Type        string

	DataContentType string
	DataSchema      *url.URL
	Subject         string
	Time            time.Time
}

const (
	DefaultCloudEventSpecVersion = "1.0"
	DefaultCloudEventType        = "ms.aio.telemetry"
)

// Attrs returns additional error attributes for slog.
func (ce *CloudEvent) Attrs() []slog.Attr {
	// Cloud events were not specified; just bail out.
	if ce == nil {
		return nil
	}

	a := make([]slog.Attr, 0, 8)

	a = append(a,
		slog.String("id", ce.ID),
		slog.String("source", ce.Source.String()),
		slog.String("specversion", ce.SpecVersion),
		slog.String("type", ce.Type),
	)

	if ce.DataContentType != "" {
		a = append(a, slog.String("datacontenttype", ce.DataContentType))
	}
	if ce.DataSchema != nil {
		a = append(a, slog.String("dataschema", ce.DataSchema.String()))
	}
	if ce.Subject != "" {
		a = append(a, slog.String("subject", ce.Subject))
	}
	if !ce.Time.IsZero() {
		a = append(a, slog.String("time", ce.Time.Format(time.RFC3339)))
	}

	return a
}

// Initialize default values in the cloud event where possible; error where not.
func cloudEventToMessage(msg *mqtt.Message, ce *CloudEvent) error {
	// Cloud events were not specified; just bail out.
	if ce == nil {
		return nil
	}

	if ce.ID != "" {
		msg.UserProperties["id"] = ce.ID
	} else {
		id, err := errutil.NewUUID()
		if err != nil {
			return err
		}
		msg.UserProperties["id"] = id
	}

	// We have reasonable defaults for all other values; source, however, is
	// both required and something the caller must specify.
	if ce.Source == nil {
		return &errors.Error{
			Message:      "source must be defined",
			Kind:         errors.ArgumentInvalid,
			PropertyName: "CloudEvent",
		}
	}
	msg.UserProperties["source"] = ce.Source.String()

	if ce.SpecVersion != "" {
		msg.UserProperties["specversion"] = ce.SpecVersion
	} else {
		msg.UserProperties["specversion"] = DefaultCloudEventSpecVersion
	}

	if ce.Type != "" {
		msg.UserProperties["type"] = ce.Type
	} else {
		msg.UserProperties["type"] = DefaultCloudEventType
	}

	if ce.DataContentType != "" {
		msg.UserProperties["datacontenttype"] = ce.DataContentType
	} else {
		msg.UserProperties["datacontenttype"] = msg.ContentType
	}

	if ce.DataSchema != nil {
		msg.UserProperties["dataschema"] = ce.DataSchema.String()
	}
	// TODO: Default schema?

	if ce.Subject != "" {
		msg.UserProperties["subject"] = ce.Subject
	} else {
		msg.UserProperties["subject"] = msg.Topic
	}

	if !ce.Time.IsZero() {
		msg.UserProperties["time"] = ce.Time.Format(time.RFC3339)
	} else {
		msg.UserProperties["time"] = time.Now().UTC().Format(time.RFC3339)
	}

	return nil
}

func cloudEventFromMessage(msg *mqtt.Message) *CloudEvent {
	var ok bool
	var err error
	ce := &CloudEvent{}

	// Parse required properties first. If any aren't present or valid, assume
	// this isn't a cloud event.
	ce.SpecVersion = msg.UserProperties["specversion"]
	if ce.SpecVersion != "1.0" {
		return nil
	}

	ce.ID, ok = msg.UserProperties["id"]
	if !ok {
		return nil
	}

	src, ok := msg.UserProperties["source"]
	if !ok {
		return nil
	}
	ce.Source, err = url.Parse(src)
	if err != nil {
		return nil
	}

	ce.Type, ok = msg.UserProperties["type"]
	if !ok {
		return nil
	}

	// Optional properties are best-effort.
	ce.DataContentType = msg.UserProperties["datacontenttype"]

	if ds, ok := msg.UserProperties["dataschema"]; ok {
		if dsp, err := url.Parse(ds); err == nil {
			ce.DataSchema = dsp
		}
	}

	ce.Subject = msg.UserProperties["subject"]

	if t, ok := msg.UserProperties["time"]; ok {
		if tp, err := iso8601.ParseString(t); err == nil {
			ce.Time = tp
		}
	}

	return ce
}
