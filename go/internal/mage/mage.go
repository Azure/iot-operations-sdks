// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mage

import (
	"bytes"
	_ "embed"
	"fmt"
	"os"

	"github.com/princjef/mageutil/bintool"
	"github.com/princjef/mageutil/shellcmd"
)

//go:embed .golangci.yml
var golangci string

//go:embed package.gotxt
var packageDoc string

var (
	golines = bintool.Must(bintool.NewGo(
		"github.com/segmentio/golines",
		"v0.12.2",
	))
	linter = bintool.Must(bintool.New(
		"golangci-lint{{.BinExt}}",
		"1.61.0",
		"https://github.com/golangci/golangci-lint/releases/download/v{{.Version}}/golangci-lint-{{.Version}}-{{.GOOS}}-{{.GOARCH}}{{.ArchiveExt}}",
	))
	documenter = bintool.Must(bintool.New(
		"gomarkdoc{{.BinExt}}",
		"1.1.0",
		"https://github.com/princjef/gomarkdoc/releases/download/v{{.Version}}/gomarkdoc_{{.Version}}_{{.GOOS}}_{{.GOARCH}}{{.ArchiveExt}}",
	))
)

// Format formats the code.
func Format() error {
	if err := golines.Ensure(); err != nil {
		return err
	}

	return golines.Command("-m 80 --no-reformat-tags -w .").Run()
}

// Lint lints the code.
func Lint() error {
	if err := linter.Ensure(); err != nil {
		return err
	}

	done, err := tmpFile(".golangci.yml", golangci)
	if err != nil {
		return err
	}
	defer done()

	return linter.Command(`run`).Run()
}

// Doc generates documents for the code.
func Doc() error {
	if err := documenter.Ensure(); err != nil {
		return err
	}

	done, err := tmpFile("package.gotxt", packageDoc)
	if err != nil {
		return err
	}
	defer done()

	return documenter.Command(
		`--template-file package=package.gotxt --output '{{.Dir}}/API.md' ./...`,
	).Run()
}

// Test runs the unit tests.
func Test() error {
	return shellcmd.Command(`go test -race -cover -timeout 30s ./...`).Run()
}

// TestClean runs the unit tests with no test cache.
func TestClean() error {
	return shellcmd.RunAll(
		`go clean -testcache`,
		`go test -race -cover -timeout 12s ./...`,
	)
}

// CI runs format, lint, doc, and test.
func CI() error {
	if err := Format(); err != nil {
		return err
	}

	if err := Lint(); err != nil {
		return err
	}

	if err := Doc(); err != nil {
		return err
	}

	return Test()
}

// Verify that no thrashing occurred.
func Verify() error {
	// Check git status for any modified files.
	modified, err := shellcmd.Command(`git ls-files -mz`).Output()
	if err != nil {
		return err
	}
	if len(modified) > 0 {
		files := bytes.Split(modified, []byte{0})
		return fmt.Errorf(
			`found modified files - %s`,
			bytes.Join(files[:len(files)-1], []byte(", ")),
		)
	}
	return nil
}

// CIVerify runs CI and verifies no thrashing occurred.
func CIVerify() error {
	if err := CI(); err != nil {
		return err
	}
	return Verify()
}

func tmpFile(name, contents string) (func(), error) {
	if err := os.WriteFile(name, []byte(contents), 0o600); err != nil {
		return nil, err
	}
	return func() { os.Remove(name) }, nil
}
