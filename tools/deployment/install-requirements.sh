#!/bin/bash

set -o errexit
set -o pipefail

# install k3d
if [ ! $(which k3d) ]
then
    wget -q -O - https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash
fi

# install helm
if [ ! $(which helm) ]
then
    curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
fi

# install step
if [ ! $(which step) ]
then
    wget https://github.com/smallstep/cli/releases/download/v0.28.0/step-cli_amd64.deb -P /tmp
    sudo dpkg -i /tmp/step-cli_amd64.deb
fi

# install az cli
if [ ! $(which az) ]
then
    curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
    az aks install-cli
fi

# install k9s
if [ ! $(which k9s) ]
then
    wget https://github.com/derailed/k9s/releases/latest/download/k9s_linux_amd64.deb -P /tmp
    sudo dpkg -i /tmp/k9s_linux_amd64.deb
fi
