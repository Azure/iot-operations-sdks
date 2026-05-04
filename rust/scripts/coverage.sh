#!/usr/bin/env bash

# DESCRIPTION: Generates a coverage summary (and optional HTML report)
# for a given Cargo manifest. Output is written to `OUTPUT_DIR` if set,
# otherwise to `<cargo target dir>/report`. Expects that `test.sh` has
# already been run with `INSTRUMENTED=1` so coverage data is present.
#
# PARAMETERS:
# - Positional:
#   - MANIFEST: OPTIONAL
#     Path to a `Cargo.toml`. If unset, uses the Cargo-inferred ambient manifest.
# - Environment:
#   - BAD: DEFAULT = "40"
#     Integer threshold for "bad" coverage percentage, below which a
#     crate will be labeled with '\u{274C}'.
#   - GOOD: DEFAULT = "70"
#     Integer threshold for "good" coverage percentage, above which a
#     crate will be labeled with '\u{2795}'. Crates which have neither
#     "good" nor "bad" coverage will be labeled with '\u{2796}'.
#   - SUMMARY_ONLY: OPTIONAL
#     If set and non-null, disables generation of the HTML report.
#   - OUTPUT_DIR: OPTIONAL
#     Absolute path for the coverage output directory. When unset,
#     defaults to `<cargo target dir>/report`. Callers that need a
#     specific location (e.g. CI jobs that upload an artifact from a
#     known path) should pass this explicitly rather than relying on
#     the script to guess.
#   - VERBOSE: OPTIONAL
#     If set and non-null, passes `--verbose` to the underlying
#     `cargo llvm-cov` report invocations.

set -euo pipefail

REPOSITORY_ROOT="$(git rev-parse --show-toplevel)"
MANIFEST="$(
    cargo locate-project \
        ${1:+--manifest-path="${1}"} \
        --message-format=plain
)"

: "${BAD:=40}" "${GOOD:=70}"

VERBOSE_FLAGS=()
if [ -n "${VERBOSE:+_}" ]; then
    VERBOSE_FLAGS+=(--verbose)
fi

if [ -n "${OUTPUT_DIR:+_}" ]; then
    OUTPUT_DIRECTORY="${OUTPUT_DIR}"
else
    TARGET="$(
        cargo metadata --format-version=1 --manifest-path="${MANIFEST}" --no-deps \
        | jq -r '.target_directory'
    )"
    OUTPUT_DIRECTORY="${TARGET}/report"
fi
rm -rf "${OUTPUT_DIRECTORY}"
mkdir -p "${OUTPUT_DIRECTORY}"

if [ -z "${SUMMARY_ONLY:+_}" ]; then
    cargo llvm-cov \
        --output-dir="${OUTPUT_DIRECTORY}" \
        --manifest-path="${MANIFEST}" \
        "${VERBOSE_FLAGS[@]}" \
        --html \
        report
fi

cargo llvm-cov --manifest-path="${MANIFEST}" "${VERBOSE_FLAGS[@]}" --summary-only --json report \
| jq -crf "${REPOSITORY_ROOT}/rust/scripts/coverage-report-format.jq" \
        --arg root "${REPOSITORY_ROOT}" \
        --arg manifest "${MANIFEST}" \
        --arg short_name "$(basename "$(dirname "${MANIFEST}")")" \
        --argjson bad "${BAD}" \
        --argjson good "${GOOD}" \
        >"${OUTPUT_DIRECTORY}/summary.md"
