# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import restthermostatconnectorapp:latest -c k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-config.yaml

# Deploy SQL server (as an asset)
kubectl apply -f ./KubernetesResources/sql-server.yaml

# if the sql server yaml cant insert data into the table
# then it needs to be done manually. Port forward needs to be done before.
# kubectl port-forward <pod-name> 1433:1433 
# sqlcmd -U sa -P "MyExtremelyStrongpassword@123" 

# For cretaing table and columns 
sqlcmd -S 127.0.0.1 -U sa -P "MyExtremelyStrongpassword@123" -i setup.sql 

 

# Deploy SQL server asset and AEP
kubectl delete -f connector-config.yaml
kubectl delete -f sql-server-asset-endpoint-profile-definition.yaml
kubectl delete -f sql-server-asset-definition.yaml

