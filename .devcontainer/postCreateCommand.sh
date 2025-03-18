#!/bin/bash

echo "Starting postCreateCommand"

if [ "$CODESPACE_NAME" ]; then
    BASE_NAME=`echo ${CODESPACE_NAME//-} | head -c 12`
else
    BASE_NAME=`echo ${HOSTNAME//-} | head -c 12`
fi

# create environment variables to support deployment
echo "
export SESSION=$PWD/.session
export CLUSTER_NAME=${BASE_NAME}
export STORAGE_ACCOUNT=${BASE_NAME}storage
export SCHEMA_REGISTRY=${BASE_NAME}schema
export SCHEMA_REGISTRY_NAMESPACE=${BASE_NAME}schemans
export PATH=$PATH:~/.dotnet/tools
" >> ~/.bashrc

# create a default resource group if not defined
if [ -z "$RESOURCE_GROUP" ]; then
    echo "export RESOURCE_GROUP=aio-${BASE_NAME}" >> ~/.bashrc
fi

# create a default location if not defined
if [ -z "$LOCATION" ]; then
    echo "export LOCATION=westus3" >> ~/.bashrc
fi

echo "Ending postCreateCommand"
