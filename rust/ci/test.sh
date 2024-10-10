#! /bin/sh

# DESCRIPTION: Checks Rust lints and runs tests for crates.
#
# PARAMETERS:
# - Positional:
#   - MANIFEST: OPTIONAL
#     Path to a `Cargo.toml`.  If unset, the script will use the
#     Cargo-inferred ambient manifest.
# - Environment:
#   - INSTRUMENTED: OPTIONAL
#     If set and non-null, builds and runs tests with LLVM coverage
#     instrumentation enabled.  This is necessary for generating a
#     coverage report with `coverage.sh`.

set -eux

run_tests() {
    # NOTE: Word splitting is required since "COMMON_OPTIONS" contains an
    # array of flags.
    cargo clippy --all --manifest-path="${MANIFEST}" \
        --features "${FEATURES:-}" \
        ${COMMON_TARGETS} \
        ${COMMON_OPTIONS} \
        -- --deny=warnings

    if [ -n "${INSTRUMENTED:+_}" ]; then
        # NOTE: Extra `--verbose` to enable verbosity in underlying Cargo
        # invocation.
        # shellcheck disable=SC2086
        cargo llvm-cov --manifest-path="${MANIFEST}" \
            --features "${FEATURES:-}" \
            --no-report \
            --verbose \
            ${COMMON_TARGETS} \
            ${COMMON_OPTIONS} \
            test
    else
        # shellcheck disable=SC2086
        cargo test --manifest-path="${MANIFEST}" \
            --features "${FEATURES:-}" \
            ${COMMON_TARGETS} \
            ${COMMON_OPTIONS}
    fi

    # Need to do a separate test run for doc because of a known issue with cargo
    # https://github.com/rust-lang/cargo/issues/6669
    cargo test --manifest-path="${MANIFEST}" \
        --features "${FEATURES:-}" \
        --doc \
        ${COMMON_OPTIONS}
}

REPOSITORY_ROOT="$(git rev-parse --show-toplevel)"

# NOTE: "CI" is used instead of "GITHUB_ACTIONS" because "build-deps.sh"
# does not use CI-specific features.
if [ "${CI:-}" = "true" ]; then
    . "${REPOSITORY_ROOT}/rust/ci/build-deps.sh"
fi

# NOTE: While this script only requires relative path to the manifest,
# running `locate-project` allows for early exit if the manifest is
# malformed.
MANIFEST="$(
    cargo locate-project \
        ${1:+--manifest-path="${1}"} \
        --message-format=plain
)"

COMMON_TARGETS="--tests --examples"

COMMON_OPTIONS=""
if [ "${CI:-}" = "true" ]; then
    COMMON_OPTIONS="${COMMON_OPTIONS} --verbose"
    if [ -e "Cargo.lock" ]; then
        # NOTE: If `Cargo.lock` exists, ensure it is up-to-date.
        COMMON_OPTIONS="${COMMON_OPTIONS} --locked"
    fi
fi

run_tests

