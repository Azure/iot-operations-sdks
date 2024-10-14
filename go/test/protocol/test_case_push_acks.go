// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type TestCasePushAcks struct {
	Publish     []TestAckKind `yaml:"publish"`
	Subscribe   []TestAckKind `yaml:"subscribe"`
	Unsubscribe []TestAckKind `yaml:"unsubscribe"`
}
