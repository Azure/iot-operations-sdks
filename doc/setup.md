# Setup

The following instructions will get you started with setting up a development environment for building the samples and creating Azure IoT Operations edge applications.

## Setup the platform

We recommend four different platform paths for developing with Azure IoT Operations which utilize [k3d](https://k3d.io/#what-is-k3d) (a lightweight [k3s](https://k3s.io/) wrapper). Codespaces provides the most streamlined experience and can get the development environment up and running in a couple of minutes.

> [!NOTE]
> For development, it's recommended to make the cluster locally available, either by deploying the cluster on the local machine, or using the the [Visual Studio Code Server](https://code.visualstudio.com/docs/remote/vscode-server) function that is used by Codespaces.

The Codespaces approach is the recommended option and it provides all the necessary tools pre-installed.

### Codespaces *(Recommended)*

1. Install [VS Code](https://code.visualstudio.com/)

1. Launch Codespaces:

    [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Azure/iot-operations-sdks?hide_repo_select=true&editor=vscode)

1. Open the codespace in VS Code Desktop (required to login to Azure):

    > **F1 > Codespaces: Open in VS Code Desktop**

### Local dev container *(Recommended)*

1. Install [VS Code](https://code.visualstudio.com/)

1. Install the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)

1. Open the *Azure IoT Operations SDKs* repository in a dev container:

    > **F1 > Dev Containers: Clone Repository in Container Volume...**

1. When prompted, enter the repository URL:

    ```
    https://github.com/azure/iot-operations-sdks
    ```

> [!TIP]
> Alternatively, if you have already cloned the *Azure Iot Operations SDKS* repository, you may open this folder directly with VS Code:
>
> **F1 > Dev Containers: Open Folder in Container...**

### Linux

1. Install [Ubuntu](https://ubuntu.com/download/desktop)

1. Install [Docker Engine](https://docs.docker.com/engine/install/ubuntu/)

> [!CAUTION]
> Ubuntu provides unofficial Docker packages via snap or apt. Install directly from Docker guarantees that latest version.

### Windows Subsystem for Linux (WSL)

1. Install Ubuntu on [WSL 2](https://learn.microsoft.com/windows/wsl/install):

    ```powershell
    wsl --install Ubuntu
    ```

1. Install [Docker Desktop with WSL 2](https://docs.docker.com/desktop/features/wsl/)

## Install prerequisites

> [!NOTE]
> Codespaces comes pre-installed with all required prerequisites. If you have deployed a codespace from the Azure IoT Operations SDKs repository, then you can skip these steps.

1. Install Git:

    ```bash
    sudo apt-get install git
    ```

1. Clone the Azure IoT Operations SDK repository:

    ```bash
    git clone https://github.com/Azure/iot-operations-sdks
    ```

## Install Azure IoT Operations

Installation of Azure IoT Operations can be performed by connecting your cluster to Azure Arc (simulating a production environment) or by installing directly to the cluster with Helm.

### Install with Azure Arc

Your Kubernetes cluster and Azure IoT Operations can be setup via Helm or via Azure Arc. Azure Arc provides the full Azure IoT Operations experience including the [Dashboard](https://iotoperations.azure.com) where you can deploy Assets.

1. Open a shell in the root directory of this repository

1. Run the init script which will install k3d (plus other dependencies) and create a new cluster:

    ```bash
    sudo ./tools/deployment/initialize-cluster.sh
    ```

1. Follow the [Learn docs](https://learn.microsoft.com/azure/iot-operations/get-started-end-to-end-sample/quickstart-deploy?tabs=codespaces) to connect your cluster to Azure Arc and deploy Azure IoT Operations.

1. [Connect your cluster](https://learn.microsoft.com/azure/iot-operations/deploy-iot-ops/howto-prepare-cluster?tabs=ubuntu#arc-enable-your-cluster)
 to Azure Arc

1. [Deploy Azure IoT Operations](https://learn.microsoft.com/azure/iot-operations/deploy-iot-ops/howto-deploy-iot-operations?tabs=cli) to your cluster

### Install with Helm

Installation via Helm allows you to get started quickly, however this is missing the Azure integration so it may not be suitable for some development.

1. Open a shell in the root directory of this repository

1. Create a new k3d cluster:

    ```bash
    sudo ./tools/deployment/initialize-cluster.sh
    ```

1. Install Azure IoT Operations:

    ```bash
    ./tools/deployment/deploy-aio.sh nightly
    ```

> [!CAUTION]
> The scripts linked above simplify the environment setup. To understand the steps, review the scripts in the [deployment directory](/tools/deployment/).

## Broker configuration

Once setup is complete, the cluster will contain the following MQTT broker definitions:

| Component | Name | Description |
|-|-|-|
| `Broker` | default | The MQTT broker |
| `BrokerListener` | default | Provides **cluster access** to the MQTT Broker:</br>Port `18883` - TLS, SAT auth |
| `BrokerListener` | default-external | Provides **external access** to the MQTT Broker:</br>Port `1883` - no TLS, no auth</br>Port `8883` - TLS, x509 auth</br>Port `8884` - TLS, SAT auth
| `BrokerAuthentication` | default | A SAT authentication definition used by the `default` BrokerListener.
| `BrokerAuthentication` | default-x509 | An x509 authentication definition used by the `default-external` BrokerListener.

## Local artifacts

As part of the deployment script, the following files are created in the local environment, to facilitate connection and authentication to the MQTT broker. These files are located in the `.session` directory, found at the repository root.

> [!NOTE]
> For applications that will be deployed to the cluster, SAT  is the preferred authentication method for connecting to the MQTT broker.

| File | Description |
|-|-|
| `broker-ca.crt` | The MQTT broker trust bundle required to validate the MQTT broker on ports `8883` and `8884`
| `token.txt` | A Service authentication token (SAT) for authenticating with the MQTT broker on `8884`
| `client.crt` | A x509 client certificate for authenticating with the MQTT broker on port `8883`
| `client.key` | A x509 client private key for authenticating with the MQTT broker on port `8883`

## Testing the Setup

To test the setup is working correctly, use `mosquitto_pub` to connect to the locally accessible MQTT broker ports to validate the x509 certs, SAT and trust bundle.

1. Export the `.session` directory:

    ```bash
    export SESSION=$(git rev-parse --show-toplevel)/.session
    ```

1. Test no TLS, no auth:

    ```bash
    mosquitto_pub -L mqtt://localhost:1883/hello -m world --debug
    ```

1. Test TLS with x509 auth:

    ```bash
    mosquitto_pub -L mqtts://localhost:8883/hello -m world --cafile $SESSION/broker-ca.crt --cert $SESSION/client.crt --key $SESSION/client.key --debug
    ```

1. Test TLS with SAT auth:

    ```bash
    mosquitto_pub -L mqtts://localhost:8884/hello -m world --cafile $SESSION/broker-ca.crt -D CONNECT authentication-method K8S-SAT -D CONNECT authentication-data $(cat $SESSION/token.txt) --debug
    ```

## Next Steps

The development environment is now setup! Refer to the language documentation for further instructions on setting up the SDK:

* Get started with the [.NET SDK ](/dotnet/)

* Get started with the [Go SDK](/go/)

* Get started with the [Rust SDK](/rust/)
