// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//go:build mage
// +build mage

package main

import (
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/mage"
)

var coverPkg = []string{
	`github.com/Azure/iot-operations-sdks/go/internal`,
	`github.com/Azure/iot-operations-sdks/go/mqtt`,
	`github.com/Azure/iot-operations-sdks/go/protocol`,
	`github.com/Azure/iot-operations-sdks/go/services`,
}

// Test runs the unit tests.
func Test() error {
	return mage.Tester{
		CoverPkg: coverPkg,
		Race:     true,
		Timeout:  30 * time.Second,
	}.Run()
}

// TestClean runs the unit tests with no test cache.
func TestClean() error {
	return mage.Tester{
		Clean:    true,
		CoverPkg: coverPkg,
		Race:     true,
		Timeout:  30 * time.Second,
	}.Run()
}

// CI runs format, lint, and test.
func CI() error {
	if err := mage.Format(); err != nil {
		return err
	}

	if err := mage.Lint(); err != nil {
		return err
	}

	return Test()
}

func CIVerify() error {
	if err := CI(); err != nil {
		return err
	}
	return mage.Verify()
}
