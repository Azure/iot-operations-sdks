#!/bin/bash

# create some environment variables to simplify deployment
echo 'export CLUSTER_NAME=${CODESPACE_NAME%-*}' >> ~/.bashrc
echo 'export STORAGE_ACCOUNT=${CLUSTER_NAME}-sa' >> ~/.bashrc
echo 'export SCHEMA_REGISTRY=${CLUSTER_NAME}-sr' >> ~/.bashrc
echo 'export SCHEMA_REGISTRY_NAMESPACE=${CLUSTER_NAME}-srn' >> ~/.bashrc
source ~/.bashrc

echo -e "Environment:\n
echo -e "\tSUBSCRIPTION_ID: $SUBSCRIPTION_ID"
echo -e "\tRESOURCE_GROUP: $RESOURCE_GROUP"
echo -e "\tLOCATION: $LOCATION"
echo -e "\tCLUSTER_NAME: $CLUSTER_NAME"
echo -e "\tSTORAGE_ACCOUNT: $STORAGE_ACCOUNT"
echo -e "\tSCHEMA_REGISTRY: $SCHEMA_REGISTRY"
echo -e "\tSCHEMA_REGISTRY_NAMESPACE: $SCHEMA_REGISTRY_NAMESPACE"

sudo sh -c 'echo 127.0.0.1 aio-broker >> /etc/hosts'
