# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import pollingrestthermostatconnector:latest -c k3s-default

# Build REST server docker image
docker build -t rest-server:latest ./SampleRestServer
docker tag rest-server:latest rest-server:latest
k3d image import rest-server:latest -c k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-config.yaml

# Deploy REST server (as an asset)
kubectl apply -f ./KubernetesResources/rest-server.yaml

# Deploy REST server asset and AEP
kubectl apply -f ./KubernetesResources/rest-server-asset-endpoint-profile-definition.yaml
kubectl apply -f ./KubernetesResources/rest-server-asset-definition.yaml
