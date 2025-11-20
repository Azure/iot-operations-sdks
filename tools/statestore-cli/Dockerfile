# Pull base image of 1.75 to be consistent with host-app
# Note: We can't use the base image of 1.89 as they are not available yet.
FROM mcr.microsoft.com/azurelinux/base/rust:1.75 AS build

# RUST_VERSION should be the value of "channel" in rust-toolchain.toml
ARG RUST_VERSION=1.89

# Move source code from build context into /app folder
COPY --link / /app
WORKDIR /app

# Force Install Rust 1.89
RUN tdnf remove -y rust; \
    tdnf install -y openssl-devel clang; \
    curl https://sh.rustup.rs -sSf | sh -s -- -y --default-toolchain $RUST_VERSION --profile minimal

RUN --mount=type=secret,id=CARGO_TOKEN,target=/run/secrets/cargo_token \
    CARGO_REGISTRIES_AIO_SDKS_TOKEN=$(cat /run/secrets/cargo_token) \
    cargo build -p "statestore-cli" --release

# Finalize image
FROM mcr.microsoft.com/azurelinux/base/core:3.0 AS final
RUN tdnf install -y shadow-utils
# Create non-root user
ARG USERNAME="aio_devs"
ARG USERGROUP="aio_dataflow"

RUN groupadd -r ${USERGROUP}
RUN useradd -g ${USERGROUP} -m -s /bin/bash $USERNAME && \
    chown -R ${USERNAME}:${USERGROUP} /home/$USERNAME
USER $USERNAME
WORKDIR /home/$USERNAME
COPY --from=build /app/target/release/statestore-cli /bin/statestore-cli
ENTRYPOINT ["/bin/statestore-cli"]