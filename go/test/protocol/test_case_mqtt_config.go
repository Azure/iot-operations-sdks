// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type TestCaseMqttConfig struct {
	ClientID *string `yaml:"client-id"`
}

func MakeTestCaseMqttConfig() TestCaseMqttConfig {
	return TestCaseMqttConfig{
		nil,
	}
}
