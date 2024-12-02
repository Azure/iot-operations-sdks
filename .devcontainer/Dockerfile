FROM mcr.microsoft.com/devcontainers/base:ubuntu

RUN apt-get update && \
    apt-get install -y --no-install-recommends mosquitto-clients && \
    wget https://github.com/smallstep/cli/releases/download/v0.28.0/step-cli_amd64.deb && \
    sudo dpkg -i step-cli_amd64.deb
