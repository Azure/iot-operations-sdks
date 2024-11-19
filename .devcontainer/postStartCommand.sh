#!/bin/bash

# create some environment variables to simplify deployment
echo 'export CLUSTER_NAME=${CODESPACE_NAME%-*}' >> ~/.bashrc
echo 'export STORAGE_ACCOUNT=${CLUSTER_NAME}-sa' >> ~/.bashrc
echo 'export SCHEMA_REGISTRY=${CLUSTER_NAME}-sr' >> ~/.bashrc
echo 'export SCHEMA_REGISTRY_NAMESPACE=${CLUSTER_NAME}-srn' >> ~/.bashrc
source ~/.bashrc

echo "Environment:
    SUBSCRIPTION_ID:           $SUBSCRIPTION_ID
    RESOURCE_GROUP:            $RESOURCE_GROUP
    LOCATION:                  $LOCATION
    CLUSTER_NAME:              $CLUSTER_NAME
    STORAGE_ACCOUNT:           $STORAGE_ACCOUNT
    SCHEMA_REGISTRY:           $SCHEMA_REGISTRY
    SCHEMA_REGISTRY_NAMESPACE: $SCHEMA_REGISTRY_NAMESPACE"

sudo sh -c 'echo 127.0.0.1 aio-broker >> /etc/hosts'
