# Tools to assist development

An overview of some of the tools for managing and interacting with the Azure IoT Operations Kubernetes cluster.

## K9s

[K9s](https://k9scli.io/) is a CLI tool that is pre-installed when setting up the development environment and provides easy access to viewing details about the cluster.

Some popular shortcuts:
* `:services` : see what services are exposed
* `:pods` : status of pods
    * press `0` to show **all namespaces**
    * press `l` to show a log of the pod
    * press `enter` to see the containers in the pod
* `:deployments` : manage deployments
    * press `e` to edit the deployment
* `:events:` : list all events on the cluster, press `shift-l` to sort by last seen.
* `:secrets:` : show cluster secrets
* `:configmap:` : show cluster config maps
* `:Broker` : list the MQTT brokers
* `:BrokerListeners` : Show the BrokerListeners

## K3s cluster details

Information about the workloads currently installed in the k3s cluster can be viewed using the `crictl` tool which can be executed from within the k3s container:

1. Open a shell in the k3s container:

    ```bash
    docker exec -it k3d-k3s-default-server-0 sh
    ```

1. List the images:

    ```bash
    crictl images
    ```
