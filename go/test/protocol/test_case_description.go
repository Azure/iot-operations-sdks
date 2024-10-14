// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type TestCaseDescription struct {
	Condition string `yaml:"condition"`
	Expect    string `yaml:"expect"`
}
