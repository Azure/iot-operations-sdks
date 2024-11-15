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

	ceID              = "id"
	ceSource          = "source"
	ceSpecVersion     = "specversion"
	ceType            = "type"
	ceDataContentType = "datacontenttype"
	ceDataSchema      = "dataschema"
	ceSubject         = "subject"
	ceTime            = "time"
)

var ceReserved = []string{
	ceID,
	ceSource,
	ceSpecVersion,
	ceType,
	ceDataContentType,
	ceDataSchema,
	ceSubject,
	ceTime,
}

// Attrs returns additional attributes for slog.
func (ce *CloudEvent) Attrs() []slog.Attr {
	// Cloud events were not specified; just bail out.
	if ce == nil {
		return nil
	}

	a := make([]slog.Attr, 0, 8)

	a = append(a,
		slog.String(ceID, ce.ID),
		slog.String(ceSource, ce.Source.String()),
		slog.String(ceSpecVersion, ce.SpecVersion),
		slog.String(ceType, ce.Type),
	)

	if ce.DataContentType != "" {
		a = append(a, slog.String(ceDataContentType, ce.DataContentType))
	}
	if ce.DataSchema != nil {
		a = append(a, slog.String(ceDataSchema, ce.DataSchema.String()))
	}
	if ce.Subject != "" {
		a = append(a, slog.String(ceSubject, ce.Subject))
	}
	if !ce.Time.IsZero() {
		a = append(a, slog.String(ceTime, ce.Time.Format(time.RFC3339)))
	}

	return a
}

// Initialize default values in the cloud event where possible; error where not.
func cloudEventToMessage(msg *mqtt.Message, ce *CloudEvent) error {
	// Cloud events were not specified; just bail out.
	if ce == nil {
		return nil
	}

	for _, key := range ceReserved {
		if _, ok := msg.UserProperties[key]; ok {
			return &errors.Error{
				Message:       "metadata key reserved for cloud event",
				Kind:          errors.ArgumentInvalid,
				PropertyName:  "Metadata",
				PropertyValue: key,
			}
		}
	}

	if ce.ID != "" {
		msg.UserProperties[ceID] = ce.ID
	} else {
		id, err := errutil.NewUUID()
		if err != nil {
			return err
		}
		msg.UserProperties[ceID] = id
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
	msg.UserProperties[ceSource] = ce.Source.String()

	if ce.SpecVersion != "" {
		msg.UserProperties[ceSpecVersion] = ce.SpecVersion
	} else {
		msg.UserProperties[ceSpecVersion] = DefaultCloudEventSpecVersion
	}

	if ce.Type != "" {
		msg.UserProperties[ceType] = ce.Type
	} else {
		msg.UserProperties[ceType] = DefaultCloudEventType
	}

	if ce.DataContentType != "" {
		msg.UserProperties[ceDataContentType] = ce.DataContentType
	} else {
		msg.UserProperties[ceDataContentType] = msg.ContentType
	}

	if ce.DataSchema != nil {
		msg.UserProperties[ceDataSchema] = ce.DataSchema.String()
	}

	if ce.Subject != "" {
		msg.UserProperties[ceSubject] = ce.Subject
	} else {
		msg.UserProperties[ceSubject] = msg.Topic
	}

	if !ce.Time.IsZero() {
		msg.UserProperties[ceTime] = ce.Time.Format(time.RFC3339)
	} else {
		msg.UserProperties[ceTime] = time.Now().UTC().Format(time.RFC3339)
	}

	return nil
}

func cloudEventFromMessage(msg *mqtt.Message) *CloudEvent {
	var ok bool
	var err error
	ce := &CloudEvent{}

	// Parse required properties first. If any aren't present or valid, assume
	// this isn't a cloud event.
	ce.SpecVersion = msg.UserProperties[ceSpecVersion]
	if ce.SpecVersion != "1.0" {
		return nil
	}

	ce.ID, ok = msg.UserProperties[ceID]
	if !ok {
		return nil
	}

	src, ok := msg.UserProperties[ceSource]
	if !ok {
		return nil
	}
	ce.Source, err = url.Parse(src)
	if err != nil {
		return nil
	}

	ce.Type, ok = msg.UserProperties[ceType]
	if !ok {
		return nil
	}

	// Optional properties are best-effort.
	ce.DataContentType = msg.UserProperties[ceDataContentType]

	if ds, ok := msg.UserProperties[ceDataSchema]; ok {
		if dsp, err := url.Parse(ds); err == nil {
			ce.DataSchema = dsp
		}
	}

	ce.Subject = msg.UserProperties[ceSubject]

	if t, ok := msg.UserProperties[ceTime]; ok {
		if tp, err := iso8601.ParseString(t); err == nil {
			ce.Time = tp
		}
	}

	return ce
}
