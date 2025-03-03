# Build the passive replication sample docker image
dotnet publish /t:PublishContainer

# Tag and push the passive replication sample docker image to the local registry
docker tag passivereplicationsample:latest k3d-registry.localhost:5000/passivereplicationsample:latest
docker push k3d-registry.localhost:5000/passivereplicationsample:latest

kubectl apply -f ./deploy.yaml