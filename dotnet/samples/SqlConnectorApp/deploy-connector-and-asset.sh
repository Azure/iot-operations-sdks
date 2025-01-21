# Build connector sample image
dotnet publish /t:PublishContainer
docker login -u dockeroliva -p "#3sLHaDpzdaD"
docker tag sqlqualityanalyzerconnectorapp:latest dockeroliva/sqlqualityanalyzerconnectorapp:latest
docker push dockeroliva/sqlqualityanalyzerconnectorapp:latest 
k3d image import sqlqualityanalyzerconnectorapp:latest -c k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-config.yaml

# Deploy SQL server (as an asset)
kubectl apply -f ./KubernetesResources/sql-server-try-this.yaml

# if the sql server yaml cant insert data into the table
# then it needs to be done manually. Port forward needs to be done before.
# kubectl port-forward <pod-name> 1433:1433 
# sqlcmd -U sa -P "MyExtremelyStrongpassword@123" 

# For cretaing table and columns 
# sqlcmd -S 127.0.0.1 -U sa -P "MyExtremelyStrongpassword@123" -i setup.sql 

kubectl apply -f ./KubernetesResources/sql-server-asset-endpoint-profile-definition.yaml
kubectl apply -f ./KubernetesResources/sql-server-asset-definition.yaml
 
# Deploy SQL server asset and AEP
kubectl delete -f connector-config.yaml
kubectl delete -f sql-server-asset-endpoint-profile-definition.yaml
kubectl delete -f sql-server-asset-definition.yaml

