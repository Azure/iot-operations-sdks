# Initializing a Kubernetes cluster and installing Azure IoT Operations

These scripts support the general [Setups](/docs/setup.md) documentation.

## Supported environments

The scripts have been tested in the following environments:

1. [CodeSpaces](https://github.com/features/codespaces) - Launch this repository directly in codespaces
1. [VSCode Dev Containers](https://code.visualstudio.com/docs/devcontainers/containers) - Create the environment in docker running on your local box using VSCode

1. WSL - Deploy on k3d installed in WSL

> [!NOTE]
> Docker will need to be preinstalled in the target environment

## Scenarios

### Setup for development

> [!CAUTION]
> `initialize-cluster.sh` will **DELETE** your default k3d cluster.

1. Initialize the cluster:

    ```bash
    ./tools/deployment/initialize-cluster.sh
    ```

2. [Install Azure IoT Operations](https://learn.microsoft.com/azure/iot-operations/deploy-iot-ops/overview-deploy)

3. Configure Azure IoT Operations for development:

    ```bash
    ./tools/deployment/deploy-aio.sh
    ```

### Update credentials

To refresh the MQTT broker authorization credentials (located in the `.session` folder), including SAT, MQTT Broker server cert and x509 client certs, run the following:

```bash
./tools/deployment/update-credentials.sh
```

## Scripts

The following is a brief outline of the function of the three major scripts in this directory.

### `initialize-cluster`

Installs prerequisites and creates a new cluster:

1. Installs prerequisites (executes `./install-requirements`) including:
    1. Install k3d
    1. Install Step CLI
    1. Helm
    1. AZ CLI
    1. Step
1. **DELETE** the existing default k3d cluster
1. Deploy a new k3d cluster
1. Set up port forwarding for ports `1883`, `8883`, and `8884` to enable TLS
1. Create a local registry

### `deploy-aio`

Configures the MQTT broker for development purposes:

1. Setup certificate services, if missing
1. Create root and intermediate CAs for x509 authentication
1. Create the trust bundle ConfigMap for the Broker to authentication x509 clients
1. Configure a `BrokerListener` and `BrokerAuthentication` resources for SAT and x509 auth

### `update-credentials`

This will download to your local machine, the different files needed to authenticate your application.

1. Download the Broker trust bundle: `.session/broker-ca.crt`
1. Create a client x509 certificate: `.session/client.crt` & `.session/client.key`
1. Create a new SAT: `.session/token.txt`
