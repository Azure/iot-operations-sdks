#!/bin/bash

# create some environment variables to simplify deployment
export BASE_NAME=${CODESPACE_NAME//-} | head -c 12
echo 'export CLUSTER_NAME=${BASE_NAME}' >> ~/.bashrc
echo 'export STORAGE_ACCOUNT=${BASE_NAME}-sa' >> ~/.bashrc
echo 'export SCHEMA_REGISTRY=${BASE_NAME}-sr' >> ~/.bashrc
echo 'export SCHEMA_REGISTRY_NAMESPACE=${BASE_NAME}-srn' >> ~/.bashrc
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
