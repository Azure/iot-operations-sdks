#!/bin/bash

set -o errexit
set -o nounset
set -o pipefail

echo "Starting onCreateCommand"

sudo cp .devcontainer/welcome.txt /usr/local/etc/vscode-dev-containers/first-run-notice.txt

# install mosquitto
sudo apt-get update 
sudo apt-get install -y --no-install-recommends mosquitto-clients

# initialize the cluster
tools/deployment/initialize-cluster.sh

echo "Ending onCreateCommand"
