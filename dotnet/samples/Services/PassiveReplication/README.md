# .NET sample application for MQTT Broker

This is a .NET sample used to demonstrate how to connect an in-cluster Pod to MQTT Broker and then perform passive replication using the leader election client.

When deployed, 2 or more pods running this hosted service will automatically campaign to be elected 
leader on start-up. Once one of the pods is elected leader, that pod will periodically update some 
shared resource in the MQ state store. The other pods will passively remain idle and wait for their opportunity to be elected leader.

For more details on how leader election works, please see the [Leader Election documentation](/dotnet/src/Azure.Iot.Operations.Services/LeaderElection/README.md).

## Build and deploy

For addition information on building and deploying containers to the cluster, refer to the [deployment documentation](/doc/edge_application/deploy.md).

1. Build the container image:

    ```bash
    dotnet publish /t:PublishContainer
    ```

1. Import the image container:

    ```bash
    k3d image import passivereplicationsample
    ```

1. Apply the deployment to the cluster:

    ```bash
    kubectl apply -f ./deploy.yaml
    ```

1. Confirm the pods are running:

    ```bash
    kubectl get pods -l app=passive-replication
    ```

    output:
    ```output
    passive-replication-deployment-6dc457dd49-dp6h8   1/1     Running   0          7s
    passive-replication-deployment-6dc457dd49-nhwg8   1/1     Running   0          7s
    ```

## Testing

### Scale up and down

1. Scale up or down the number of pods by updating the replication count in 
[deploy.yaml](./deploy.yaml).

    ```yaml
    spec:
      replicas: 5  # Increase from 2 nodes to 5
    ```

1. Deploy the changes to the cluster:

    ```bash
    kubectl apply -f ./deploy.yaml
    ```

1. Observe the changes to number of pods:

    ```bash
    kubectl get pods -l app=passive-replication
    ```

    output:
    ```output
    NAME                                              READY   STATUS    RESTARTS     AGE
    passive-replication-deployment-7dcb59d48b-cmdj9   1/1     Running   0            2m36s
    passive-replication-deployment-7dcb59d48b-fvhdl   1/1     Running   1 (2s ago)   4s
    passive-replication-deployment-7dcb59d48b-j6sp4   1/1     Running   0            4s
    passive-replication-deployment-7dcb59d48b-kdpgg   1/1     Running   0            2m35s
    passive-replication-deployment-7dcb59d48b-v24cr   1/1     Running   0            4s    
    ```

### Node disruption

A node distruption can be simulated by deleting the pod containing the leader node.

Once that pod is deleted, Kubernetes will automatically re-create a new pod (to match the replica count defined in [deploy.yaml](./deploy.yaml)), but this disruption should provide enough time for a new leader to be elected.
