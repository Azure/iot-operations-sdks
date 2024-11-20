#!/bin/bash

export BASE_NAME=`echo ${CODESPACE_NAME//-} | head -c 12`

# create environment variables to support deployment
echo "export CLUSTER_NAME=${BASE_NAME}
export STORAGE_ACCOUNT=${BASE_NAME}storage
export SCHEMA_REGISTRY=${BASE_NAME}schema
export SCHEMA_REGISTRY_NAMESPACE=${BASE_NAME}schemans
export SESSION=${CODESPACE_VSCODE_FOLDER}/.session" >> ~/.bashrc

# create a default resource group if not defined
if [ -z "$RESOURCE_GROUP" ]; then
    echo "export RESOURCE_GROUP=aio-${BASE_NAME}" >> ~/.bashrc
fi

# create a default location if not defined
if [ -z "$LOCATION" ]; then
    echo "export LOCATION=westus3" >> ~/.bashrc
fi

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
