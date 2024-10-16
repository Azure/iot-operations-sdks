// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type TestCaseSync struct {
	SignalEvent *string `yaml:"signal-event"`
	WaitEvent   *string `yaml:"wait-event"`
}
