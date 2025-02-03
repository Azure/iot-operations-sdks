# Delete a cluster if it already exists
k3d cluster delete

k3d registry delete registry.localhost

# Create a local registry to hold the passive replication sample image that will be built next
k3d registry create registry.localhost --port 5000

# Start the k8s cluster that will use the local registry
k3d cluster create --registry-use k3d-registry.localhost:5000

# Deploy MQ 
# Note that this sample requires MQ version 0.5.0 or greater.
helm install broker oci://mqbuilds.azurecr.io/helm/aio-broker --version 0.7.0-nightly  --set global.quickstart=true

# Build the passive replication sample docker image
dotnet publish /t:PublishContainer

# Tag and push the passive replication sample docker image to the local registry
docker tag passivereplicationsample:latest k3d-registry.localhost:5000/passivereplicationsample:latest
docker push k3d-registry.localhost:5000/passivereplicationsample:latest

kubectl apply -f ./deploy.yaml
