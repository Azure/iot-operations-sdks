// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type TestCaseCloudEvent struct {
	Source          *string `yaml:"source"`
	Type            *string `yaml:"type"`
	SpecVersion     *string `yaml:"spec-version"`
	DataContentType *string `yaml:"data-content-type"`
	Subject         *string `yaml:"subject"`
	DataSchema      *string `yaml:"data-schema"`
}
