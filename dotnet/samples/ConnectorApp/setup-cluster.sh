# Create k3d cluster with local image registry
../../../tools/deployment/initialize-cluster.sh

# Deploy Broker
../../../tools/deployment/deploy-aio.sh nightly

# Deploy ADR
helm install adrcommonprp --version 0.3.0 oci://azureadr.azurecr.io/helm/adr/common/adr-crds-prp -n azure-iot-operations --wait

# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import httpconnectorworkerservice:latest -c k3s-default

# Build HTTP server docker image
docker build -t http-server:latest ./SampleHttpServer
docker tag http-server:latest http-server:latest
k3d image import http-server:latest -c k3s-default

# Deploy Operator helm chart
helm install akri-operator oci://akribuilds.azurecr.io/helm/microsoft-managed-akri-operator --version 0.4.0-main-20241016.1-buddy -n azure-iot-operations --wait

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-config.yaml

# Deploy HTTP connector secrets
kubectl apply -f ./KubernetesResources/http-connector-secrets.yaml

# Deploy HTTP server (as an asset)
kubectl apply -f ./SampleHttpServer/http-server.yaml

# Deploy HTTP server asset and AEP
kubectl apply -f ./KubernetesResources/http-server-asset-endpoint-profile-definition.yaml
# This doesn't do anything yet
#kubectl apply -f ./KubernetesResources/http-server-asset-definition.yaml
