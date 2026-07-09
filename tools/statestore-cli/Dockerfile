# Pull base image of 1.75 to be consistent with host-app
# Note: We can't use the base image of 1.89 as they are not available yet.
FROM mcr.microsoft.com/azurelinux/base/rust:1.75 AS build

# RUST_VERSION should be the value of "channel" in rust-toolchain.toml
ARG RUST_VERSION=1.88

# Move source code from build context into /app folder
COPY --link / /app
WORKDIR /app

# Force Install Rust 1.88
RUN tdnf remove -y rust; \
    tdnf install -y openssl-devel clang; \
    curl https://sh.rustup.rs -sSf | sh -s -- -y --default-toolchain $RUST_VERSION --profile minimal

# AIO SDK deps resolve from public crates.io (no aio-sdks feed), so no CARGO_TOKEN secret is needed.
RUN cargo build -p "statestore-cli" --release

# Finalize image
FROM mcr.microsoft.com/azurelinux/base/core:3.0 AS final
# Refresh base-image OS packages to latest Azure Linux 3.0 patches (clears base-image CVEs)
RUN tdnf -y update && tdnf install -y shadow-utils && tdnf clean all -y
# Create non-root user
ARG USERNAME="aio_devs"
ARG USERGROUP="aio_dataflow"

RUN groupadd -r ${USERGROUP} && \
    useradd -r -g ${USERGROUP} ${USERNAME}

USER ${USERNAME}
COPY --from=build --chown=${USERNAME}:${USERGROUP} /app/target/release/statestore-cli /opt/app/statestore-cli
ENTRYPOINT ["/opt/app/statestore-cli"]