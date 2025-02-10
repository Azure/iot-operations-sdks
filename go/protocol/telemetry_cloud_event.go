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
	// ceDataContentType - not stored in user properties, so omitted here.
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
func (ce *CloudEvent) toMessage(msg *mqtt.Message) error {
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

	if ce.DataContentType != "" && ce.DataContentType != msg.ContentType {
		return &errors.Error{
			Message:       "cloud event content type mismatch",
			Kind:          errors.ArgumentInvalid,
			PropertyName:  "DataContentType",
			PropertyValue: ce.DataContentType,
		}
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

// CloudEventFromTelemetry extracts cloud event data from the given telemetry
// message. It will return an error if any required properties are missing or
// any properties do not match the expected schema.
func CloudEventFromTelemetry[T any](
	msg *TelemetryMessage[T],
) (*CloudEvent, error) {
	var ok bool
	var err error
	ce := &CloudEvent{}

	ce.SpecVersion, ok = msg.Metadata[ceSpecVersion]
	if !ok {
		return nil, &errors.Error{
			Message:    "cloud event missing spec version header",
			Kind:       errors.HeaderMissing,
			HeaderName: ceSpecVersion,
		}
	}
	if ce.SpecVersion != "1.0" {
		return nil, &errors.Error{
			Message:     "cloud event invalid spec version",
			Kind:        errors.HeaderInvalid,
			HeaderName:  ceSpecVersion,
			HeaderValue: ce.SpecVersion,
		}
	}

	ce.ID, ok = msg.Metadata[ceID]
	if !ok {
		return nil, &errors.Error{
			Message:    "cloud event missing ID header",
			Kind:       errors.HeaderMissing,
			HeaderName: ceID,
		}
	}

	src, ok := msg.Metadata[ceSource]
	if !ok {
		return nil, &errors.Error{
			Message:    "cloud event missing source header",
			Kind:       errors.HeaderMissing,
			HeaderName: ceSource,
		}
	}
	ce.Source, err = url.Parse(src)
	if err != nil {
		return nil, &errors.Error{
			Message:     "cloud event invalid source header",
			Kind:        errors.HeaderInvalid,
			HeaderName:  ceSource,
			HeaderValue: src,
		}
	}

	ce.Type, ok = msg.Metadata[ceType]
	if !ok {
		return nil, &errors.Error{
			Message:    "cloud event missing type header",
			Kind:       errors.HeaderMissing,
			HeaderName: ceType,
		}
	}

	// Don't fail for missing optional properties, but do fail for optional
	// properties that don't parse.
	ce.DataContentType = msg.ContentType

	if ds, ok := msg.Metadata[ceDataSchema]; ok {
		ce.DataSchema, err = url.Parse(ds)
		if err != nil {
			return nil, &errors.Error{
				Message:     "cloud event invalid data schema header",
				Kind:        errors.HeaderInvalid,
				HeaderName:  ceDataSchema,
				HeaderValue: ds,
			}
		}
	}

	ce.Subject = msg.Metadata[ceSubject]

	if t, ok := msg.Metadata[ceTime]; ok {
		ce.Time, err = iso8601.ParseString(t)
		if err != nil {
			return nil, &errors.Error{
				Message:     "cloud event invalid time header",
				Kind:        errors.HeaderInvalid,
				HeaderName:  ceTime,
				HeaderValue: t,
			}
		}
	}

	return ce, nil
}
