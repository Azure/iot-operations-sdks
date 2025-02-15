# Download the latest release (change version number if needed)
PROTOC_VERSION=25.2  # Check https://github.com/protocolbuffers/protobuf/releases/latest for the latest
curl -LO https://github.com/protocolbuffers/protobuf/releases/download/v${PROTOC_VERSION}/protoc-${PROTOC_VERSION}-linux-x86_64.zip

# Extract the binary
unzip protoc-${PROTOC_VERSION}-linux-x86_64.zip -d $HOME/.local

# Add `protoc` to PATH
export PATH="$HOME/.local/bin:$PATH"

# Verify installation
protoc --version
