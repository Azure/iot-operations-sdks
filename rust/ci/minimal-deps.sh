# WARN: This script is destructive to your machine's environment and
# globally-installed files.

# NOTE: This script is meant to be sourced, and so is not executable.
# Set the shellcheck shell instead of using a shebang since the latter
# would be counterintuitive.
# shellcheck shell=sh

# NOTE: This script is sourced by exit-on-error scripts, so it is not
# necessary to exit if `cd` fails.  Disable the lint checking this.
# shellcheck disable=SC2164

_curl() {
    curl --proto '=https' --tlsv1.2 --location "${@}"
}

export CARGO_INCREMENTAL="0"
export DEBIAN_FRONTEND="noninteractive"
export TZ="UTC"

RUSTUP_TARGET="${RUSTUP_TARGET:-"$(
    ARCH="$(uname -m)"
    if [ "${ARCH}" != x86_64 ] && [ "${ARCH}" != aarch64 ]; then
        printf "UNSUPPORTED ARCHITECTURE\n"
        exit 1
    fi
    # NOTE: Assuming Debian derivatives use glibc.
    printf "%s-unknown-linux-gnu" "${ARCH}"
)"}"

sudo apt-get update
sudo apt-get install -y curl git jq

CARGO_BIN="${HOME}/.cargo/bin"
export PATH="${PATH}:${CARGO_BIN}"

if ! command -v rustup; then
    (
        TEMP_DEST="$(mktemp -d)"
        trap 'rm -rf "${TEMP_DEST}"' EXIT
        cd "${TEMP_DEST}"

        _curl --output rustup "https://static.rust-lang.org/rustup/dist/${RUSTUP_TARGET}/rustup-init"
        _curl "https://static.rust-lang.org/rustup/dist/${RUSTUP_TARGET}/rustup-init.sha256" \
        | sed -e 's/\*.*/rustup/' \
        | sha256sum -c -

        mkdir -p "${CARGO_BIN}"
        mv rustup "${CARGO_BIN}"
        chmod +x "${CARGO_BIN}/rustup"
    )
    hash -r
fi

# NOTE: If rustup was already installed, make sure it's up-to-date.  If
# it was just installed above, create the hardlinks for cargo, rustc,
# etc.
# UPDATE 3/3/2025: self update broke with version 1.28 and associated behavioral changes.
# For now, disable update as a mitigation, revisit after updating to Rust 2024 edition
# and when the changes for rustup 1.28 are stabilized for CI.
#
# Ref: https://www.linuxcompatible.org/story/rustup-1280-released/
#rustup self update
rustup set auto-self-update disable

# NOTE: The toolchain specified by rust-toolchain will be automatically
# installed if it doesn't already exist when `cargo` is run later.  We'd
# like rustup to use the minimal profile to do that so that it doesn't
# use the default profile and download rust-docs, etc.
#
# Ref: https://github.com/rust-lang/rustup/issues/2579
rustup set profile minimal
