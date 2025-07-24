#!/bin/bash

set -o errexit
set -o nounset
set -o pipefail

echo "Starting onCreateCommand"

sudo cp .devcontainer/welcome.txt /usr/local/etc/vscode-dev-containers/first-run-notice.txt

# initialize the cluster
tools/deployment/initialize-cluster.sh -y

echo "Ending onCreateCommand"
