# .NET sample application for MQ

This is a .NET sample used to demonstrate how to connect an in-cluster Pod using MQTTnet to MQ and
then perform passive replication using the Akir.MQ leader election client.

This sample is designed to be deployed alongside an MQ deployment. 

When deployed, 2 or more pods running this hosted service will automatically campaign to be elected 
leader on start-up. Once one of the pods is elected leader, that pod will periodically update some 
shared resource in the MQ state store. The other pod(s) will passively remain idle and wait for their opportunity to be elected leader.

For more details on how leader election works, please see [here](../../../lib/dotnet/src/Azure.Iot.Operations.Services/LeaderElection/README.md).

## Pre-requisites

- [.NET 7 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
- [K3D](https://k3d.io/)
- [Docker](https://docs.docker.com/engine/install/)

## Setup script

Run [this script](./setup.sh) to build and run everything necessary for this sample. 

```bash
bash ./setup.sh
```

For more details, or to deploy this sample one step at time, follow the instructions below.

## Step-by-step instructions

### Create K8s cluster with local container registry and IoT MQ

```bash
# Create a local registry to hold the passive replication sample image that will be built next
k3d registry create registry.localhost --port 5500

# Start the k8s cluster that will use the local registry
k3d cluster create --registry-use k3d-registry.localhost:5500

# Deploy MQ 
# Note that this sample requires MQ version 0.5.0 or greater.
helm install mq oci://edgebuilds.azurecr.io/helm/mq --version 0.6.0-nightly  --set global.quickstart=true
```

### Build and push the Docker image to local container registry

Once the sample code is configured to connect to your MQ instance, build the docker image with the following command

```bash
dotnet publish /t:PublishContainer
```

Once the docker image is built, tag and push it to the local registry

```bash
# Tag and push the passive replication sample docker image to the local registry
docker tag passivereplicationsample:latest k3d-registry.localhost:5500/passivereplicationsample:latest
docker push k3d-registry.localhost:5500/passivereplicationsample:latest
```

If you changed the name of the docker image or are using a different container registry than was used in this document, update the supplied [deployment file](./deploy.yaml) file to use your container registry and your docker image for the `spec.containers.image` value. The default values in this deployment file will work otherwise.

```yaml
containers:
- name: passive-replication-sample
  image: k3d-registry.localhost:5500/passivereplicationsample:latest
```

Additionally, you may need to change the authentication credentials and/or port that the MQTT client will connect to MQ over depending on your MQ instance's configuration. Be sure
to edit the relevant connection details in [deploy.yaml](./deploy.yaml) before building the docker image.

```yaml
stringData:
  passive-replication-connection-string: HostName=aio-mq-dmqtt-frontend;TcpPort=1883;UseTls=false;UserName=\$sat;PasswordFile=/var/run/secrets/tokens/mqtt-client-token
```

(Optional) Choose the number of replicas to deploy by changing the value in [deploy.yaml](./deploy.yaml). By default, there are 2 replicas deployed.

```yaml
spec:
  replicas: 2  # The number of passive replication pods to deploy
```

### Deploy the passive replication pods

```bash
kubectl apply -f ./deploy.yaml
```

See the deployment of 1 or more pods vying to be elected leader:

```bash
kubectl get pods
```

This should list off 2 (or more, if you scaled up further earlier) pods with names like:

```
passive-replication-deployment-6dc457dd49-dp6h8   1/1     Running   0          7s
passive-replication-deployment-6dc457dd49-nhwg8   1/1     Running   0          7s
```

View the logs of one of these pods to see if it was elected leader:

```bash
kubectl logs passive-replication-deployment-__________-_____
```

### Scale up and down

Even after the initial deployment, you can change the replication count by changing the value in 
[deploy.yaml](./deploy.yaml).

```yaml
spec:
  replicas: 5  # Increase from 2 nodes to 5
```

After making a change to the deployment file, simply run

```bash
kubectl apply -f ./deploy.yaml
```

And you should see more/fewer pods participating in the passive replication.

### Simulate node disruption

If you'd like to simulate the leader node crashing to see how the passive nodes respond, try out

```bash
kubectl delete pod passive-replication-deployment-__________-_____
```

where the passive-replication-deployment pod's name matches the name of the current leader.

Once that pod is deleted, kubernetes will automatically re-create a new pod (to make sure
that the currently deployed count matches the replica count set in [deploy.yaml](./deploy.yaml)),
but this disruption will provide enough time for a new leader to be elected.

After deleting the previous leader's pod, check the logs for the non-disrupted pods and you should 
see a new node elected leader.

```bash
kubectl logs passive-replication-deployment-__________-_____
```

### Cleanup deployment

You can delete the deployment you created and all the pods it spawned:

```bash
kubectl delete deployment passive-replication-deployment
```