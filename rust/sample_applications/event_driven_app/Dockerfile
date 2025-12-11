# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Build application
FROM mcr.microsoft.com/azurelinux/base/rust:1 AS build
WORKDIR /build
COPY . .
RUN cargo build -p input_client -p output_client

# Build runtime image
FROM mcr.microsoft.com/azurelinux/distroless/base:3.0
WORKDIR /
RUN apt update; apt install -y libssl3
COPY --from=build /build/target/debug/input_client .
COPY --from=build /build/target/debug/output_client .
CMD /input_client
