FROM mcr.microsoft.com/devcontainers/universal:2-linux

RUN apt-get update && \
    apt-get install -y --no-install-recommends mosquitto-clients && \
    wget --no-verbose -O /usr/local/bin/k3d https://github.com/k3d-io/k3d/releases/latest/download/k3d-linux-amd64 && \
    chmod +x /usr/local/bin/k3d && \
    wget https://github.com/derailed/k9s/releases/latest/download/k9s_linux_amd64.deb && \
    sudo dpkg -i k9s_linux_amd64.deb && \
    wget https://dl.smallstep.com/cli/docs-cli-install/latest/step-cli_amd64.deb && \
    sudo dpkg -i step-cli_amd64.deb
