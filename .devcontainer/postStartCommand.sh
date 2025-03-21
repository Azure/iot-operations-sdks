#!/bin/bash

echo "Starting postStartCommand"

echo "Environment variables:
    SUBSCRIPTION_ID:           $SUBSCRIPTION_ID
    RESOURCE_GROUP:            $RESOURCE_GROUP
    LOCATION:                  $LOCATION
    CLUSTER_NAME:              $CLUSTER_NAME
    STORAGE_ACCOUNT:           $STORAGE_ACCOUNT
    SCHEMA_REGISTRY:           $SCHEMA_REGISTRY
    SCHEMA_REGISTRY_NAMESPACE: $SCHEMA_REGISTRY_NAMESPACE"

# Add a convenience alias for the aio-broker
sudo sh -c 'echo 127.0.0.1 aio-broker >> /etc/hosts'

# Set the workspaces as a safe directory
git config --global --add safe.directory /workspaces

# Stop and start the cluster, so its in a fresh state
k3d cluster stop
k3d cluster start --wait

echo "Ending postStartCommand"
