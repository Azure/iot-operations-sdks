# WARN: This script is destructive to your machine's environment and
# globally-installed files.

# NOTE: This script is meant to be sourced, and so is not executable.
# Set the shellcheck shell instead of using a shebang since the latter
# would be counterintuitive.
# shellcheck shell=sh

# WARN: `git` availability is assumed, but this is not generally
# correct.  For example, exported sources (in a `.tar.gz`, for example)
# would cause this command to fail.
REPOSITORY_ROOT="$(git rev-parse --show-toplevel)"

# NOTE: Split variable assignment and substitution so that errors
# are properly propagated.
. "${REPOSITORY_ROOT}/rust/ci/minimal-deps.sh"

sudo apt-get install -y \
    build-essential cmake libssl-dev perl pkg-config protobuf-compiler

cargo install \
    --version '^0.5' \
    --locked \
    cargo-llvm-cov

cargo install \
    --version '^0.6' \
    --locked \
    cargo-machete
