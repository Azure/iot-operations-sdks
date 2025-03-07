# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import sqlqualityanalyzerconnectorapp:latest -c k3s-default

# Deploy SQL server (for the asset)
kubectl apply -f ./KubernetesResources/sql-server.yaml

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-config.yaml

# Deploy asset and AEP
kubectl apply -f ./KubernetesResources/sql-server-asset-endpoint-profile-definition.yaml
kubectl apply -f ./KubernetesResources/sql-server-asset-definition.yaml
 
# Delete SQL server asset and AEP
# kubectl delete -f ./KubernetesResources/connector-config.yaml
# kubectl delete -f ./KubernetesResources/sql-server-asset-endpoint-profile-definition.yaml
# kubectl delete -f ./KubernetesResources/sql-server-asset-definition.yaml
