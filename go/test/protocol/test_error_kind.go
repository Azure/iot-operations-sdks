// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"errors"

	"gopkg.in/yaml.v3"
)

type TestErrorKind int

const (
	None TestErrorKind = iota
	Content
	Execution
)

func (k TestErrorKind) String() string {
	return [...]string{"None", "Content", "Execution"}[k]
}

func (k *TestErrorKind) UnmarshalYAML(value *yaml.Node) error {
	if value.Kind != yaml.ScalarNode {
		return errors.New("TestErrorKind must be ScalarNode")
	}

	switch value.Value {
	default:
		return errors.New("unrecognized TestErrorKind")
	case "none":
		*k = None
		return nil
	case "content":
		*k = Content
		return nil
	case "execution":
		*k = Execution
		return nil
	}
}
