# Initializing a Kubernetes cluster and installing Azure IoT Operations

## initialize-cluster

1. Install k3d
1. Install Step CLI
1. Delete any existing k3d cluster
1. Deploy a new k3d cluster
1. Set up port forwarding for potrts `1883`, `8883`, and `8884` to enable TLS
1. Create a local registry with the specified addresses


## deploy-aio

1. Install Azure IoT Operations
1. Install Jetstack to manage certs
1. Create the trust bundle ConfigMap for the broker
1. Create a client crt/key pair in the repository root for authenticating the client samples
1. Deploy the `Broker` resource