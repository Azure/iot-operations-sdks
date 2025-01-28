# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import sqlqualityanalyzerconnectorapp:latest -c k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-config.yaml

# Deploy SQL server (for the asset)
kubectl apply -f ./KubernetesResources/sql-server-try-this.yaml

## CHECK THAT DATA EXISTS , NEED PASSWORD FOR SA IN THIS STEP
## If the sql server yaml cant insert data into the table
## then it needs to be done manually. Port forward needs to be done before.
# kubectl port-forward -n azure-iot-operations $(kubectl get pods -n azure-iot-operations -l app=mssql -o jsonpath='{.items[0].metadata.name}') 1433:1433

# For creating table and columns and data 
# sqlcmd -S 127.0.0.1 -U sa -P "<SA_PASSWORD>" -i setup.sql 

kubectl apply -f ./KubernetesResources/sql-server-asset-endpoint-profile-definition.yaml
kubectl apply -f ./KubernetesResources/sql-server-asset-definition.yaml
 
# Delete SQL server asset and AEP
# kubectl delete -f ./KubernetesResources/connector-config.yaml
# kubectl delete -f ./KubernetesResources/sql-server-asset-endpoint-profile-definition.yaml
# kubectl delete -f ./KubernetesResources/sql-server-asset-definition.yaml

