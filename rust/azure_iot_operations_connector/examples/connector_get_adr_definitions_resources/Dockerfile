# Use the official Rust image as a base
FROM rust:1 AS build
 
# Set the working directory to /app
# WORKDIR home/vaavva/repos/iot-operations-sdks/rust
WORKDIR rust
 
# COPY . .
 
# Copy the Cargo.toml file into the working directory
# COPY Cargo.toml .
 
RUN apt update; apt install -y libssl3
RUN apt-get update && apt-get install -y --no-install-recommends libssl-dev openssl && apt-get clean
 
# # Build the Rust application
# RUN cargo build --release
 
# Copy the built application into the working directory
COPY sample_connector/release/examples .
 
# Run the command to start the application when the container is launched
CMD ["./deployed_azure_device_registry"]