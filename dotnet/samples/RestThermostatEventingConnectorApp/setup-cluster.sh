# Create k3d cluster with local image registry
../../../tools/deployment/initialize-cluster.sh

# Deploy Broker
../../../tools/deployment/deploy-aio.sh nightly

./deploy-connector-and-asset.sh
