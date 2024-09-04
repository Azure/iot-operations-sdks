#! /usr/bin/env bash

# DESCRIPTION: Generates a coverage summary (and optional HTML report)
# for a given Cargo manifest, placing the output in a subdirectory of
# the workspace target directory.
#
# If there is a coverage_threshold.toml file in a crate, this script will error
# if the thresholds are violated. # The coverage_threshold.toml file should be
# placed in the root directory of a project containing crates for which you want
# to enforce coverage thresholds. The file should have the following format:
#
# "crate_name" = coverage_threshold
#
# - "crate_name" is the name of the crate with quotes and any subdirectory path
#   (if applicable), e.g., "obs-lib/obs-lib-macros".
# - coverage_threshold is a numeric value (integer or decimal) without quotes,
#   representing the minimum coverage percentage.
#
# Example:
# "diagnostics-service" = 80
# "mock-otlp-receiver" = 65
# "obs-lib" = 80
# "obs-lib/obs-lib-macros" = 75
# "slo/diagnostics-probe" = 70
# "slo/slo-lib" = 80
#
# In this example, the coverage for "diagnostics-service" must be at least 80%,
# "mock-otlp-receiver" must be at least 65%, etc. The coverage_threshold.toml
# file only supports whole number coverage marks.
#
# PARAMETERS:
# - Positional:
#   - MANIFEST: OPTIONAL
#     Path to a `Cargo.toml`.  If unset, the script will use the
#     Cargo-inferred ambient manifest.
# - Environment:
#   - BAD: DEFAULT = "40"
#     Integer threshold for "bad" coverage percentage, below which a
#     crate will be labeled with '\u{274C}'.
#   - GOOD: DEFAULT = "70"
#     Integer threshold for "good" coverage percentage, above which a
#     crate will be labeled with '\u{2795}'.  Crates which have neither
#     "good" nor "bad" coverage will be labeled with '\u{2796}'.
#   - SUMMARY_ONLY: OPTIONAL
#     If set and non-null, disables generation of the HTML report.
#   - TARGET_KEY: DEFAULT = "report"
#     Subdirectory of the workspace target directory in which to place
#     the coverage summary and optional HTML report.

set -euxo pipefail

REPOSITORY_ROOT="$(git rev-parse --show-toplevel)"
MANIFEST="$(
    cargo locate-project \
        ${1:+--manifest-path="${1}"} \
        --message-format=plain
)"

: "${BAD:=40}" "${GOOD:=70}" "${TARGET_KEY:=report}"

if [ "${GITHUB_ACTIONS:-}" = "true" ]; then
    OUTPUT_DIRECTORY="${RUNNER_TEMP}/${TARGET_KEY}"
else
    TARGET="$(
        cargo metadata --format-version=1 --manifest-path="${MANIFEST}" --no-deps \
        | jq -r '.target_directory'
    )"
    OUTPUT_DIRECTORY="${TARGET}/${TARGET_KEY}"
fi
rm -rf "${OUTPUT_DIRECTORY}"
mkdir -p "${OUTPUT_DIRECTORY}"

if [ -z "${SUMMARY_ONLY:+_}" ]; then
    cargo llvm-cov \
        --output-dir="${OUTPUT_DIRECTORY}" \
        --manifest-path="${MANIFEST}" \
        --verbose \
        --html \
        report
fi

cargo llvm-cov --manifest-path="${MANIFEST}" --verbose --summary-only --json report \
| jq -crf "${REPOSITORY_ROOT}/rust/ci/jq/coverage.jq" \
        --arg root "${REPOSITORY_ROOT}" \
        --arg manifest "${MANIFEST}" \
        --arg short_name "$(basename "$(dirname "${MANIFEST}")")" \
        --argjson bad "${BAD}" \
        --argjson good "${GOOD}" \
        >"${OUTPUT_DIRECTORY}/summary.md"

# Fail the coverage for any crates below the coverage thresholds
crate_dir=$(dirname "$MANIFEST")
threshold_file="$crate_dir/coverage_threshold.toml"

if [ -f "$threshold_file" ]; then
    grep -Eo '^\s*[^#][^=]+=\s*[0-9]+' "$threshold_file" | while read -r line; do
        # Remove leading and trailing spaces and quotes from the crate name and the threshold value
        crate_name=$(echo "$line" | awk -F= '{gsub(/^[ \t]+|[ \t]+$/, "", $1); print $1}')
        coverage_threshold=$(echo "$line" | awk -F= '{gsub(/^[ \t]+|[ \t]+$/, "", $2); print $2}')

        coverage_percentage=$(grep -oP "${crate_name//\"}/\s*\|\s*\K[0-9.]+%" "${OUTPUT_DIRECTORY}/summary.md" | tr -d '%')

        if (( $(echo "${coverage_percentage} < ${coverage_threshold}" | bc -l) )); then
            echo "ERROR: Coverage for crate '${crate_name}' is below the threshold (${coverage_percentage}% < ${coverage_threshold}%)."
            exit 1
        fi
    done
fi
