dotnet publish ../SimpleTelemetry /t:PublishContainer
k3d image import simple-telemetry-tester:latest -c k3s-default
kubectl apply -f ./telemetry-pod.yaml
