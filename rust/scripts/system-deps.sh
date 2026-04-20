#!/usr/bin/env bash

# DESCRIPTION: Installs OS-level (apt) dependencies required to build
# the Rust workspace. Consumed by CI and local dev.
#
# WARN: Destructive to the machine's environment.
#
# Assumptions:
# - Debian-derivative distro with apt-get
# - sudo is available (or script is run as root)
#
# See also: `cargo-tools.sh` for third-party cargo subcommands
# (cargo-machete, cargo-llvm-cov). Those are not installed here because
# CI installs them via `taiki-e/install-action` (prebuilt binaries) and
# local/devcontainer flows install them via `cargo install`.

set -euo pipefail

# Prevent interactive prompts from package post-install scripts (e.g.
# tzdata asking for a timezone region) that would hang CI or a fresh
# devcontainer build. `apt-get -y` only suppresses apt's own yes/no
# prompt, not debconf prompts from individual packages.
export DEBIAN_FRONTEND=noninteractive

# System deps needed to build `openssl-sys` dynamically (everything else
# in the workspace is pure Rust or has no extra system requirement).
sudo apt-get update
sudo apt-get install -y \
    build-essential \
    libssl-dev \
    pkg-config \
    curl \
    git \
    jq
