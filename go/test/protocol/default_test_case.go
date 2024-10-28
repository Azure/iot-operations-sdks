// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultTestCase struct {
	Prologue DefaultPrologue `toml:"prologue"`
	Actions  DefaultAction   `toml:"actions"`
}
