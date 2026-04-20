#!/usr/bin/env bash

# DESCRIPTION: Installs third-party cargo subcommands used by `make
# check` and `make coverage` (cargo-machete, cargo-llvm-cov). These are
# community tools published to crates.io, not rustup components, so
# they must be installed separately from the Rust toolchain.
#
# Intended for local development. CI does NOT use this script: the CI
# workflow installs these tools via `taiki-e/install-action`, which
# downloads prebuilt binaries much faster than `cargo install` can
# compile them from source.
#
# Assumptions:
# - A Rust toolchain (cargo) is already on PATH
# - ~/.cargo/bin is on PATH
#
# See also: `system-deps.sh` for apt-level dependencies.

set -euo pipefail

if ! command -v cargo >/dev/null 2>&1; then
    echo "error: cargo not found on PATH; install the Rust toolchain first" >&2
    exit 1
fi

if ! command -v cargo-llvm-cov >/dev/null 2>&1; then
    cargo install --version '^0.5' --locked cargo-llvm-cov
fi

if ! command -v cargo-machete >/dev/null 2>&1; then
    cargo install --version '^0.7' --locked cargo-machete
fi
