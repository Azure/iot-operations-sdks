#!/bin/bash

echo "Starting postStartCommand"

echo "Environment:
    SUBSCRIPTION_ID:           $SUBSCRIPTION_ID
    RESOURCE_GROUP:            $RESOURCE_GROUP
    LOCATION:                  $LOCATION
    CLUSTER_NAME:              $CLUSTER_NAME
    STORAGE_ACCOUNT:           $STORAGE_ACCOUNT
    SCHEMA_REGISTRY:           $SCHEMA_REGISTRY
    SCHEMA_REGISTRY_NAMESPACE: $SCHEMA_REGISTRY_NAMESPACE"

# Add a convenience alias for the aio-broker
sudo sh -c 'echo 127.0.0.1 aio-broker >> /etc/hosts'

echo "Ending postStartCommand"
