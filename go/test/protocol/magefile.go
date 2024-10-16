//go:build mage
// +build mage

package main

//mage:import
import (
	"github.com/Azure/iot-operations-sdks/go/internal/mage"
	"github.com/princjef/mageutil/shellcmd"
)

// Test runs the unit tests.
func Test() error {
	// Cannot use -race, since the wallclock tests register as racy.
	return shellcmd.Command(`go test`).Run()
}

// TestClean runs the unit tests with no test cache.
func TestClean() error {
	// Cannot use -race, since the wallclock tests register as racy.
	return shellcmd.RunAll(`go clean -testcache`, `go test`)
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
