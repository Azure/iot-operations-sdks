// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"errors"

	"gopkg.in/yaml.v3"
)

type TestFeatureKind int

const (
	Unobtanium TestFeatureKind = iota
	AckOrdering
	Reconnection
	Caching
	Dispatch
	ExplicitDefault
)

func (k TestFeatureKind) String() string {
	return [...]string{"Unobtanium", "AckOrdering", "Reconnection", "Caching", "Dispatch", "ExplicitDefault"}[k]
}

func (k *TestFeatureKind) UnmarshalYAML(value *yaml.Node) error {
	if value.Kind != yaml.ScalarNode {
		return errors.New("TestFeatureKind must be ScalarNode")
	}

	switch value.Value {
	default:
		return errors.New("unrecognized TestFeatureKind")
	case "unobtanium":
		*k = Unobtanium
		return nil
	case "ack-ordering":
		*k = AckOrdering
		return nil
	case "reconnection":
		*k = Reconnection
		return nil
	case "caching":
		*k = Caching
		return nil
	case "dispatch":
		*k = Dispatch
		return nil
	case "explicit-default":
		*k = ExplicitDefault
		return nil
	}
}
