// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type TestCase struct {
	TestName    string              `yaml:"test-name"`
	Description TestCaseDescription `yaml:"description"`
	Requires    []TestFeatureKind   `yaml:"requires"`
	Prologue    TestCasePrologue    `yaml:"prologue"`
	Actions     []TestCaseAction    `yaml:"actions"`
	Epilogue    TestCaseEpilogue    `yaml:"epilogue"`
}
