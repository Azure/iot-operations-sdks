CARGO := cargo
SHELL := /usr/bin/env bash

CARGO_PROFILE := $(if $(value RELEASE),--release,)
TARGET_SUBDIRECTORY := $(if $(value RELEASE),release,debug)

REPOSITORY_ROOT := $(shell git rev-parse --show-toplevel)

.PHONY: check coverage default test test-all

default:

check:
	exec $(REPOSITORY_ROOT)/rust/ci/check.sh

test:
	$(eval export FEATURES INSTRUMENTED MANIFEST)
	exec $(REPOSITORY_ROOT)/rust/ci/test.sh "$${MANIFEST}"

# NOTE: `export ${VAR} := ..` is an option, but `$(eval export ..)` is
# used to make target arguments clearer.
coverage: INSTRUMENTED := 1
coverage: $(if $(value SKIP_TEST),,test)
	$(eval export BAD GOOD MANIFEST SUMMARY_ONLY TARGET_KEY)
	exec $(REPOSITORY_ROOT)/rust/ci/coverage.sh "$${MANIFEST}"
