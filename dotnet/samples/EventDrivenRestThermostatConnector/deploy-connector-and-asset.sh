# Build TCP thermostat client app
dotnet publish ../SampleTcpServiceApp /t:PublishContainer
k3d image import sampletcpclientapp:latest -c k3s-default

# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import eventdrivenrestthermostatconnector:latest -c k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-config.yaml

# Deploy REST server (as an asset)
kubectl apply -f ./KubernetesResources/tcp-client.yaml

# Deploy REST server asset and AEP
kubectl apply -f ./KubernetesResources/tcp-client-asset-endpoint-profile-definition.yaml
kubectl apply -f ./KubernetesResources/tcp-client-asset-definition.yaml
