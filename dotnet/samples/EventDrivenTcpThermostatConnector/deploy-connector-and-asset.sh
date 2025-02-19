# Build TCP thermostat client app
dotnet publish ../SampleTcpServiceApp /t:PublishContainer
k3d image import sampletcpserviceapp:latest -c k3s-default

# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import eventdriventcpthermostatconnector:latest -c k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-config.yaml

# Deploy TCP server (as an asset)
kubectl apply -f ./KubernetesResources/tcp-service.yaml

# Deploy TCP server asset and AEP
kubectl apply -f ./KubernetesResources/tcp-service-asset-endpoint-profile-definition.yaml
kubectl apply -f ./KubernetesResources/tcp-service-asset-definition.yaml
