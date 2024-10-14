// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"time"
)

type TestCaseDuration struct {
	Hours        int `yaml:"hours" toml:"hours"`
	Minutes      int `yaml:"minutes" toml:"minutes"`
	Seconds      int `yaml:"seconds" toml:"seconds"`
	Milliseconds int `yaml:"milliseconds" toml:"milliseconds"`
	Microseconds int `yaml:"microseconds" toml:"microseconds"`
}

func (tcd TestCaseDuration) ToDuration() time.Duration {
	return time.Duration(
		tcd.Hours,
	)*time.Hour + time.Duration(
		tcd.Minutes,
	)*time.Minute + time.Duration(
		tcd.Seconds,
	)*time.Second + time.Duration(
		tcd.Milliseconds,
	)*time.Millisecond + time.Duration(
		tcd.Microseconds,
	)*time.Microsecond
}
