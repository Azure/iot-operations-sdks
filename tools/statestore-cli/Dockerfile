# RUST_VERSION should be the value of "channel" in rust-toolchain.toml
ARG RUST_VERSION=1.85
FROM mcr.microsoft.com/azurelinux/base/rust:$RUST_VERSION AS build

# Move source code from build context into /app folder
COPY --link / /app
WORKDIR /app

# Install additional deps
RUN tdnf install -y openssl-devel clang

RUN --mount=type=secret,id=CARGO_TOKEN,target=/run/secrets/cargo_token \
    CARGO_REGISTRIES_AIO_SDKS_TOKEN=$(cat /run/secrets/cargo_token) \
    cargo build -p "statestore-cli" --release

# Finalize image
FROM mcr.microsoft.com/azurelinux/distroless/base:3.0 AS final
COPY --from=build /app/target/release/statestore-cli /bin/statestore-cli
ENTRYPOINT ["/bin/statestore-cli"]