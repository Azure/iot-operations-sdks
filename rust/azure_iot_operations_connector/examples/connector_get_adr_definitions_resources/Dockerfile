# Use the official Rust image as a base
FROM rust:1 AS build

# Set the working directory to /rust
WORKDIR rust

RUN apt update; apt install -y libssl3
RUN apt-get update && apt-get install -y --no-install-recommends libssl-dev openssl && apt-get clean

# Copy the built application into the working directory
COPY target_connector_get_adr_definitions/release/examples .

# Run the command to start the application when the container is launched
CMD ["./connector_get_adr_definitions"]