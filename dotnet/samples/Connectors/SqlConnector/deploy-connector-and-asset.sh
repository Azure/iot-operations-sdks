# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import sqlqualityanalyzerconnectorapp:latest -c k3s-default

# Deploy SQL server (for the asset)
kubectl apply -f ./KubernetesResources/sql-server.yaml

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-config.yaml

# Deploy device and its lone asset
kubectl apply -f ./KubernetesResources/sql-server-device-definition.yaml
kubectl apply -f ./KubernetesResources/sql-server-asset-definition.yaml
