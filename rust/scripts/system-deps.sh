#!/usr/bin/env bash

# DESCRIPTION: Installs OS-level (apt) dependencies required to build
# the Rust workspace. Consumed by CI and local dev.
#
# WARN: Destructive to the machine's environment.
#
# Assumptions:
# - Debian-derivative distro with apt-get
# - Either runs as root, or `sudo` is on PATH
#
# See also: `cargo-tools.sh` for third-party cargo subcommands
# (cargo-machete, cargo-llvm-cov). Those are not installed here because
# CI installs them via `taiki-e/install-action` (prebuilt binaries) and
# local/devcontainer flows install them via `cargo install`.

set -euo pipefail

# Pick up `sudo` only when needed: skip it when already root, fail
# fast with a clear message when neither root nor sudo is available
# (better than apt's "permission denied" or sudo's "command not
# found").
SUDO=
if [ "$(id -u)" -ne 0 ]; then
    if command -v sudo >/dev/null 2>&1; then
        SUDO=sudo
    else
        echo "error: this script needs root privileges (run as root or install sudo)" >&2
        exit 1
    fi
fi

# `DEBIAN_FRONTEND=noninteractive` prevents package post-install scripts
# (e.g. tzdata asking for a timezone region) from hanging CI or a fresh
# devcontainer build. `apt-get -y` only suppresses apt's own yes/no
# prompt, not debconf prompts from individual packages. Forwarded
# explicitly via `sudo VAR=val` rather than relying on `sudo -E` /
# sudoers `env_keep`, which aren't portable across distros.
APT_ENV=(DEBIAN_FRONTEND=noninteractive)

# System deps needed to build `openssl-sys` dynamically (everything else
# in the workspace is pure Rust or has no extra system requirement).
$SUDO "${APT_ENV[@]}" apt-get update
$SUDO "${APT_ENV[@]}" apt-get install -y \
    build-essential \
    libssl-dev \
    pkg-config \
    curl \
    git \
    jq
