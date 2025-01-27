# Build connector sample image
dotnet publish /t:PublishContainer
# docker login -u dockeroliva -p "#3sLHaDpzdaD"
# docker tag sqlqualityanalyzerconnectorapp:v12 dockeroliva/sqlqualityanalyzerconnectorapp:v12
# docker push dockeroliva/sqlqualityanalyzerconnectorapp:v12 
k3d image import sqlqualityanalyzerconnectorapp:latest -c k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-config.yaml

# Deploy SQL server (for the asset)
kubectl apply -f ./KubernetesResources/sql-server-try-this.yaml

# if the sql server yaml cant insert data into the table
# then it needs to be done manually. Port forward needs to be done before.
# kubectl port-forward $(kubectl get pods -l app=<deployment-name> -o jsonpath='{.items[0].metadata.name}') 1433:1433
# kubectl port-forward <pod-name> 1433:1433 
# sqlcmd -U sa -P "MyExtremelyStrongpassword@123" 

# For cretaing table and columns 
# sqlcmd -S 127.0.0.1 -U sa -P "MyExtremelyStrongpassword@123" -i setup.sql 

kubectl apply -f ./KubernetesResources/sql-server-asset-endpoint-profile-definition.yaml
kubectl apply -f ./KubernetesResources/sql-server-asset-definition.yaml
 
# Delete SQL server asset and AEP
# kubectl delete -f ./KubernetesResources/connector-config.yaml
# kubectl delete -f ./KubernetesResources/sql-server-asset-endpoint-profile-definition.yaml
# kubectl delete -f ./KubernetesResources/sql-server-asset-definition.yaml

