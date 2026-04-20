#!/usr/bin/env bash

# DESCRIPTION: Runs the Rust test suite for a given Cargo manifest.
# All tests are run with `--all-features` (every feature on every
# crate). Does NOT run static analysis (fmt/clippy/doc) — see
# `check.sh`.
#
# PARAMETERS:
# - Positional:
#   - MANIFEST: OPTIONAL
#     Path to a `Cargo.toml`. If unset, uses the Cargo-inferred ambient manifest.
# - Environment:
#   - INSTRUMENTED: OPTIONAL
#     If set and non-null, runs tests under `cargo llvm-cov` with LLVM
#     coverage instrumentation enabled, producing data for `coverage.sh`.
#   - ENABLE_NETWORK_TESTS: OPTIONAL
#     If set, network-dependent tests will execute. Without this, network
#     tests short-circuit inside their test bodies (legacy behavior;
#     tracked for replacement with `#[ignore]`).
#   - VERBOSE: OPTIONAL
#     If set and non-null, passes `--verbose` to cargo so every rustc
#     invocation and test binary path is logged. Useful in CI logs and
#     when diagnosing local build failures.

set -euo pipefail

MANIFEST="$(
    cargo locate-project \
        ${1:+--manifest-path="${1}"} \
        --message-format=plain
)"

COMMON_TARGETS=(--tests --examples)
COMMON_OPTIONS=(--all-features)
if [ -n "${VERBOSE:+_}" ]; then
    COMMON_OPTIONS+=(--verbose)
fi

if [ -n "${INSTRUMENTED:+_}" ]; then
    # `cargo llvm-cov test` already includes doc tests when coverage data
    # is available for them.
    cargo llvm-cov --manifest-path="${MANIFEST}" \
        --no-report \
        --verbose \
        "${COMMON_TARGETS[@]}" \
        "${COMMON_OPTIONS[@]}" \
        test
else
    cargo test --manifest-path="${MANIFEST}" \
        "${COMMON_TARGETS[@]}" \
        "${COMMON_OPTIONS[@]}"

    # Doc tests must be run in a separate invocation from regular tests.
    # Ref: https://github.com/rust-lang/cargo/issues/6669
    cargo test --manifest-path="${MANIFEST}" \
        --doc \
        "${COMMON_OPTIONS[@]}"
fi
