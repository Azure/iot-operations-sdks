# Use debug image from Azure Linux distroless
FROM mcr.microsoft.com/azurelinux/distroless/debug:3.0

# Add busybox shortcuts for rational debugging
RUN ["busybox", "--install"]

# Add a symlink to enable SSL certs
RUN mkdir /etc/ssl && ln -s /etc/pki/tls/certs /etc/ssl/certs

# Copy the built application into the working directory
COPY ./target/release/sample_connector_scaffolding .

# Run the command to start the application when the container is launched
CMD ["./sample_connector_scaffolding"]
