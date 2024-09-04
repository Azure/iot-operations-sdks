#! /usr/bin/env bash

# DESCRIPTION: Ensures the following properties for Git-tracked files
# reachable from the current directory:
# - Files (except for SVGs, see below) MUST have ending newlines
# - Files MUST NOT have lines with trailing whitespace
# - Rust sources MUST include the Microsoft copyright header
# - Rust sources MUST be formatted with `rustfmt`
# - Rust crate manifest files MUST contain:
#   ```toml
#   [lints]
#   workspace = true
#   ```
#
# PARAMETERS: NONE

set -euxo pipefail

REPOSITORY_ROOT="$(git rev-parse --show-toplevel)"
CURDIR="$(pwd -P)"

# NOTE: "CI" is used instead of "GITHUB_ACTIONS" because "build-deps.sh"
# does not use CI-specific features.
if [ "${CI:-}" = "true" ]; then
    . "${REPOSITORY_ROOT}/rust/ci/build-deps.sh"
fi

# NOTE: This script uses null-terminated strings and whole lines; unset
# IFS.
IFS=

# NOTE: Require ending newlines.
git ls-files -z -- '*.rs' \
| while read -r -d '' FILE; do \
    test -z "$(tail -c 1 "${FILE}")"
done

HEADER="$(
cat <<EOF
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
EOF
)"

# NOTE: Require copyright headers.
git ls-files -z -- '*.rs' \
| while read -r -d '' FILE; do \
    if head -n 1 "${FILE}" | grep -q 'This is an auto-generated file.  Do not modify'; then
        continue
    fi

    test "$(head -n 2 "${FILE}")" = "${HEADER}"
done

 
# NOTE: Require standard lints.
cargo metadata --format-version=1 --no-deps \
| jq -r '.packages[].manifest_path | select(startswith($cwd))' \
    --arg cwd "${CURDIR}" \
| while read -r FILE; do
    # Ideally we would use `-L` here to print the filename that doesn't contain the pattern.
    # But Ubuntu 20.04 uses grep 3.4 which has a broken behavior of `-L`:
    # it exits with code 1 if the file doesn't contain a match.
    # This was fixed in grep 3.5.
    grep -q $'^\[lints\]\nworkspace = true$' "${FILE}"
done

# NOTE: Require `rustfmt` pass.
cargo fmt --verbose --all --check

# NOTE: Check for unused dependencies.
cargo machete

