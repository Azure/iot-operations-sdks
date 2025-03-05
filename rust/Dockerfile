FROM rust:1.82 AS build
WORKDIR /work

# Build application
COPY . .
RUN cargo build

# Build runtime image
FROM alpine:3
WORKDIR /
COPY --from=build work/target/debug/counter_client ./rust-application
ENTRYPOINT ["/rust-application"]
