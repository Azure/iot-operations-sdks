# Use the official Rust image as a base
FROM rust:1 AS build

# Set the working directory to /rust
WORKDIR rust

# Copy the built application into the working directory
COPY target_connector_get_adr_definitions/release/examples .

# Run the command to start the application when the container is launched
CMD ["./connector_get_adr_definitions"]
