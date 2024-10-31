# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import httpthermostatconnectorapp:latest -c k3s-default

# Build HTTP server docker image
docker build -t http-server:latest ./SampleHttpServer
docker tag http-server:latest http-server:latest
k3d image import http-server:latest -c k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-config.yaml

# Deploy HTTP connector secrets
kubectl apply -f ./KubernetesResources/http-connector-secrets.yaml

# Deploy HTTP server (as an asset)
kubectl apply -f ./KubernetesResources/http-server.yaml

# Deploy HTTP server asset and AEP
kubectl apply -f ./KubernetesResources/http-server-asset-endpoint-profile-definition.yaml
kubectl apply -f ./KubernetesResources/http-server-asset-definition.yaml
