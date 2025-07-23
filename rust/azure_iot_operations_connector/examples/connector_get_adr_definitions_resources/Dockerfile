# Use distroless/debug for the debug image in order to keep it as close to the
# final image as possible; this also acts as the "build" stage for tasks that
# cannot be completed in the final image
FROM mcr.microsoft.com/azurelinux/distroless/debug:3.0 as debug

# Add busybox shortcuts for rational debugging
RUN ["busybox", "--install"]

# Add a symlink to enable SSL certs
RUN mkdir /etc/ssl && ln -s /etc/pki/tls/certs /etc/ssl/certs

# Copy the built application into the working directory
COPY ./target/release/examples .
RUN chmod +x ./connector_get_adr_definitions

# Run the command to start the application when the container is launched
CMD ["./connector_get_adr_definitions"]

# Switch to distroless/base for the final image
FROM mcr.microsoft.com/azurelinux/distroless/base:3.0

COPY --from=debug /etc/ssl/certs /etc/ssl/certs
COPY --from=debug base_connector_sample .

# Run the command to start the application when the container is launched
CMD ["./connector_get_adr_definitions"]
