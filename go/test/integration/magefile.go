// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//go:build mage
// +build mage

package main

import "github.com/Azure/iot-operations-sdks/go/internal/mage"

// CI runs format, lint, and test.
func CI() error {
	if err := mage.Format(); err != nil {
		return err
	}

	if err := mage.Lint(); err != nil {
		return err
	}

	return mage.Test()
}

func CIVerify() error {
	if err := CI(); err != nil {
		return err
	}
	return mage.Verify()
}
