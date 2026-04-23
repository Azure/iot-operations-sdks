SHELL := /usr/bin/env bash

REPOSITORY_ROOT := $(shell git rev-parse --show-toplevel)

.PHONY: default help check test coverage

default: help

help:
	@echo "Usage: make <target> [VAR=value ...]"
	@echo ""
	@echo "Targets:"
	@echo "  check                       Run static analysis for the SDK (fmt, clippy, copyright, machete)"
	@echo "  test [OPTIONS]              Run tests for the SDK"
	@echo "  coverage [OPTIONS]          Generate coverage report for the SDK (runs instrumented tests)"
	@echo ""
	@echo "test options:"
	@echo "  ENABLE_NETWORK_TESTS=true   Include tests that require a broker"
	@echo "  VERBOSE=1                   Pass --verbose to cargo"
	@echo ""
	@echo "coverage options (test options also apply):"
	@echo "  OUTPUT_DIR=<path>           Directory for the report (default: <target>/report)"
	@echo "  SUMMARY_ONLY=1              Skip HTML render; only produce summary.md"
	@echo "  SKIP_TEST=1                 Reuse the last instrumented test run"
	@echo "  BAD=<int>                   Threshold below which a crate is flagged (default 40)"
	@echo "  GOOD=<int>                  Threshold above which a crate is marked good (default 70)"

check:
	exec $(REPOSITORY_ROOT)/rust/scripts/check.sh

# Only export variables that have a value. An unconditional `export FOO`
# in Make exports `FOO=` (empty string) when the variable is unset, which
# is observably different from "not set" for tools that gate on presence
# (e.g. `std::env::var(...).is_err()` in Rust returns `Err` only when the
# variable is absent, not when it is empty).
# TODO: If our process for gating network tests change, this can be simplified.
test:
	$(foreach v,INSTRUMENTED MANIFEST ENABLE_NETWORK_TESTS VERBOSE,$(if $(value $v),$(eval export $v)))
	exec $(REPOSITORY_ROOT)/rust/scripts/test.sh "$${MANIFEST}"

# `coverage` runs the test suite with instrumentation, then builds the report.
# Set SKIP_TEST to reuse the last test run.
coverage: INSTRUMENTED := 1
coverage: $(if $(value SKIP_TEST),,test)
	$(foreach v,BAD GOOD MANIFEST OUTPUT_DIR SUMMARY_ONLY VERBOSE,$(if $(value $v),$(eval export $v)))
	exec $(REPOSITORY_ROOT)/rust/scripts/coverage.sh "$${MANIFEST}"
