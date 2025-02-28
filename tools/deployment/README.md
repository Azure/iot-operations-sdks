# Initializing a Kubernetes cluster and installing Azure IoT Operations

The scripts support the general [Setups](/docs/setup.md) documentation.

## Supported environments

The scripts have been tested in the following environments:

1. [CodeSpaces](https://github.com/features/codespaces) - Launch this repository directly in codespaces
1. [VSCode Dev Containers](https://code.visualstudio.com/docs/devcontainers/containers) - Create the environment in docker running on your local box using VSCode

1. WSL - Deploy on k3d installed in WSL

> [!NOTE]
> Docker will need to be preinstalled in the target environment

## Scripts

The following is a brief outline of the function of the three major scripts in this directory.

### `initialize-cluster`

1. Installs prerequisities (executes `./install-requirements`) including:
    1. Install k3d
    1. Install Step CLI
    1. Helm
    1. AZ CLI
    1. Step
1. **Delete** any existing k3d cluster
1. Deploy a new k3d cluster
1. Set up port forwarding for ports `1883`, `8883`, and `8884` to enable TLS
1. Create a local registry

### `deploy-aio`

Deploy all AIO prerequisites and configure the Broker ready for development.

1. Install the required AZ CLI extensions
1. If nightly:
    1. Install cert-manager/trust-manager for cert management
    1. Install MQTT Broker, ADR & Akri services
1. Create the trust bundle ConfigMap for the Broker to authentication x509 clients
1. Configure a `BrokerListener` and `BrokerAuthentication` resources for SAT and x509 auth
1. Runs the `update-credentials` script

### `update-credentials`

This will download to your local machine, the different files needed to authenticate your application.

1. Download the Broker trust bundle: `.session/broker-ca.crt`
1. Create a client x509 certificate: `.session/client.crt` + `.session/client.key`
1. Create a new SAT: `.session/token.txt`

## Scenarios

### Initial install

For the initial install, run the following:

```bash
./tools/deployment/initialize-cluster.sh
./tools/deployment/deploy-aio.sh nightly
```

> [!CAUTION]
> `initialize-cluster.sh` will **DELETE** your default k3d cluster.

### Update MQTT Broker

To update MQTT broker to the latest nightly, run the following:

```bash
./tools/deployment/deploy-aio.sh nightly
```

### Refresh local credentials

If you need to refresh the local credentials (in the `.session` folder), including SAT, Broker server certand x509 client certs, run the following:

```bash
./tools/deployment/update-credentials.sh
```
