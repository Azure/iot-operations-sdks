#!/bin/bash

echo "Starting postCreateCommand"

BASE_NAME=`echo ${CODESPACE_NAME//-} | head -c 12`

# create environment variables to support deployment
echo "CLUSTER_NAME=${BASE_NAME}
STORAGE_ACCOUNT=${BASE_NAME}storage
SCHEMA_REGISTRY=${BASE_NAME}schema
SCHEMA_REGISTRY_NAMESPACE=${BASE_NAME}schemans
SESSION=${CODESPACE_VSCODE_FOLDER}/.session" >> ~/.bashrc

# create a default resource group if not defined
if [ -z "$RESOURCE_GROUP" ]; then
    echo "RESOURCE_GROUP=aio-${BASE_NAME}" >> ~/.bashrc
fi

# create a default location if not defined
if [ -z "$LOCATION" ]; then
    echo "LOCATION=westus3" >> ~/.bashrc
fi

echo "Ending postCreateCommand"
