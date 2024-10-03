package iso

import (
	"bytes"
	"encoding/base64"
	"strings"
	"time"

	"github.com/relvacode/iso8601"
	"github.com/sosodev/duration"
)

// Wrappers for the native Go time types that will serialize to ISO 8601.
type (
	// Date is a date in ISO 8601 format, per RFC 3339.
	Date time.Time

	// DateTime is a date and time in ISO 8601 format, per RFC 3339.
	DateTime time.Time

	// Duration is a duration in ISO 8601 format.
	Duration time.Duration

	// Time is a time in ISO 8601 format, per RFC 3339.
	Time time.Time
)

// Wrapper for the native Go byte slice that will serialize to Base64.
type ByteSlice []byte

// full-date (0) and full-time (1) schemas, as defined by RFC3339 section 5.6.
var (
	format = strings.Split(time.RFC3339, "T")
	zeroes = strings.Split(time.Time{}.Format(time.RFC3339), "T")
)

// String returns the date to an ISO 8601 string.
func (d Date) String() string {
	return time.Time(d).Format(format[0])
}

// MarshalText marshals the date to an ISO 8601 string.
func (d Date) MarshalText() ([]byte, error) {
	return []byte(d.String()), nil
}

// UnmarshalText unmarshals the date from an ISO 8601 string.
func (d *Date) UnmarshalText(b []byte) error {
	if !bytes.ContainsAny(b, "tT") {
		b = []byte(string(b) + "T" + zeroes[1])
	}
	parsed, err := iso8601.Parse(b)
	if err != nil {
		return err
	}
	*d = Date(parsed)
	return nil
}

// String returns the date-time to an ISO 8601 string.
func (dt DateTime) String() string {
	return time.Time(dt).Format(time.RFC3339)
}

// MarshalText marshals the date-time to an ISO 8601 string.
func (dt DateTime) MarshalText() ([]byte, error) {
	return []byte(dt.String()), nil
}

// UnmarshalText unmarshals the date-time from an ISO 8601 string.
func (dt *DateTime) UnmarshalText(b []byte) error {
	parsed, err := iso8601.Parse(b)
	if err != nil {
		return err
	}
	*dt = DateTime(parsed)
	return nil
}

// String returns the duration to an ISO 8601 string.
func (d Duration) String() string {
	return duration.Format(time.Duration(d))
}

// MarshalText marshals the duration to an ISO 8601 string.
func (d Duration) MarshalText() ([]byte, error) {
	return []byte(d.String()), nil
}

// UnmarshalText unmarshals the duration from an ISO 8601 string.
func (d *Duration) UnmarshalText(b []byte) error {
	parsed, err := duration.Parse(string(b))
	if err != nil {
		return err
	}
	*d = Duration(parsed.ToTimeDuration())
	return nil
}

// String returns the time to an ISO 8601 string.
func (t Time) String() string {
	return time.Time(t).Format(format[1])
}

// MarshalText marshals the time to an ISO 8601 string.
func (t Time) MarshalText() ([]byte, error) {
	return []byte(t.String()), nil
}

// UnmarshalText unmarshals the time from an ISO 8601 string.
func (t *Time) UnmarshalText(b []byte) error {
	if !bytes.ContainsAny(b, "tT") {
		b = []byte(zeroes[0] + "T" + string(b))
	}
	parsed, err := iso8601.Parse(b)
	if err != nil {
		return err
	}
	*t = Time(parsed)
	return nil
}

// MarshalText marshals the byte slice to a Base64 string.
func (byteSlice ByteSlice) MarshalText() ([]byte, error) {
	return []byte(base64.StdEncoding.EncodeToString(byteSlice)), nil
}

// UnmarshalText unmarshals the byte slice from a Base64 string.
func (byteSlice *ByteSlice) UnmarshalText(b []byte) error {
	var err error
	*byteSlice, err = base64.StdEncoding.DecodeString(string(b))
	return err
}
