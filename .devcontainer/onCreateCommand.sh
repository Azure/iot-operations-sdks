#!/bin/bash

sudo cp .devcontainer/welcome.txt /usr/local/etc/vscode-dev-containers/first-run-notice.txt

# initialize the cluster
tools/deployment/initialize-cluster.sh
