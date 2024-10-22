// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"errors"

	"gopkg.in/yaml.v3"
)

type TestAckKind int

const (
	Success TestAckKind = iota
	Fail
	Drop
)

func (k TestAckKind) String() string {
	return [...]string{"Success", "Fail", "Drop"}[k]
}

func (k *TestAckKind) UnmarshalYAML(value *yaml.Node) error {
	if value.Kind != yaml.ScalarNode {
		return errors.New("TestAckKind must be ScalarNode")
	}

	switch value.Value {
	default:
		return errors.New("unrecognized TestAckKind")
	case "success":
		*k = Success
		return nil
	case "fail":
		*k = Fail
		return nil
	case "drop":
		*k = Drop
		return nil
	}
}
