# Setup

The following instructions will get your started with setting up a development environment for building the samples and creating Azure IoT Operations applications.

## Setup the Platform

We recommend three different platform paths for developing with Azure IoT Operations, all of which are utilize [k3d](https://k3d.io/#what-is-k3d) (a [k3s](https://k3s.io/) wrapper). Codespaces provides the most streamlined experience and can get the development environment up and running in a couple of minutes.

### Codespaces

The easiest way to get started:

1. [Install VS Code](https://code.visualstudio.com/). This is required to correctly authenticate with Azure.

1. Launch Codespaces:

    [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Azure/iot-operations-sdks?hide_repo_select=true&editor=vscode)

1. Open the codespace in VS Code Desktop  (**Ctrl + Shift + P > Codespaces: Open in VS Code Desktop**).  This is required to login to Azure in a later step.

### Linux

1. We have tested the installation steps below using the latest [Ubuntu](https://ubuntu.com/#get-ubuntu) LTS.

### Linux on Windows (WSL)

1. Install [WSL](https://learn.microsoft.com/windows/wsl/install)

1. If you already use WSL, make sure your using [WSL 2](https://learn.microsoft.com/windows/wsl/install#upgrade-version-from-wsl-1-to-wsl-2).

## Install with Azure Arc

Your Kubernetes cluster and Azure IoT Operations can be setup via Helm or via Azure Arc. Azure Arc provides the full Azure IoT Operations experience including the [Dashboard](https://iotoperations.azure.com) where you can deploy need Assets.

1. Make sure the shell is in the root directory of this repository

1. Run the init script which will install k3d and create a new cluster:

    ```bash
    ./tools/deployment/initialize-cluster.sh
    ```

1. Follow the [Learn docs](https://learn.microsoft.com/azure/iot-operations/get-started-end-to-end-sample/quickstart-deploy?tabs=codespaces) to connect your cluster to Azure Arc and deploy Azure IoT Operations.

1. [Connect your cluster](https://learn.microsoft.com/azure/iot-operations/deploy-iot-ops/howto-prepare-cluster?tabs=ubuntu#arc-enable-your-cluster)
 to Azure Arc

1. [Deploy Azure IoT Operations](https://learn.microsoft.com/azure/iot-operations/deploy-iot-ops/howto-deploy-iot-operations?tabs=cli) to your cluster]

## Install with Helm

Installation via Helm provides allows you to get started quicker, however this is missing the Azure integration so may not be suitable for some development.

1. Make sure the shell is in the root directory of this repository

1. Create a new k3d cluster:

    ```bash
    ./tools/deployment/initialize-cluster.sh
    ```

1. [Install Helm](https://helm.sh/docs/intro/install/)

1. Install Azure IoT Operations:

    ```bash
    ./tools/deployment/deploy-aio.sh nightly
    ```

Scripts can be executed with the above commands for ease of use, however if you would like to see the exact steps being performed or would like more information on he process, navigate to the [deployment folder](/tools/deployment/).

## What gets Installed?

The cluster will contain the following:

| Component | Name | Description |
|-|-|-|
| `Broker` | default | A standard MQTT broker installation |
| `BrokerListener` | default | Provides cluster access to the MQTT Broker:</br>Port `18883` - TLS, SAT auth |
| `BrokerListener` | default-external | Provides external access to the MQTT Broker:</br>Port `1883` - no TLS, no auth</br>Port `8883` - TLS, x509 auth</br>Port `8884` - TLS, SAT auth
| `BrokerAuthentication` | default | A SAT authentication definition used by the `default` BrokerListener.
| `BrokerAuthentication` | default-x509 | An x509 authentication definition used by the `default-external` BrokerListener.

It also creates the following artifacts in the local environments. These files can be found in the `.session` directory found at the repository root:

| File | Description |
|-|-|
| `broker-ca.crt` | The MQTT broker trust bundle required to validate the MQTT broker on ports `8883` and `8884`
| `client.crt` | A client certificate for accessing the MQTT broker on port `8883`
| `client.key` | A client private key for accessing the MQTT broker on port `8883`
| `token.txt` | A Service authentication token (SAT) for accessing port `8884`

## Testing the Setup

The easiest way to test the setup is working correctly is to use `mosquitto_pub` to attempt to connect to the locally accessible MQTT broker ports to validate the x509 certs, SAT and trust bundle.

1. Export the `.session` directory:

    ```bash
    export SESSION=$(git rev-parse --show-toplevel)/.session
    ```

1. Test no TLS, no auth:

    ```bash
    mosquitto_pub -L mqtt://localhost:1883/hello -m world --debug
    ```

1. Test TLS with SAT auth:

    ```bash
    mosquitto_pub -L mqtts://localhost:8884/hello -m world --cafile $SESSION/broker-ca.crt -D CONNECT authentication-method K8S-SAT -D CONNECT authentication-data $(cat $SESSION/token.txt) --debug
    ```

1. Test TLS with x509 auth:

    ```bash
    mosquitto_pub -L mqtts://localhost:8883/hello -m world --cafile $SESSION/broker-ca.crt --cert $SESSION/client.crt --key $SESSION/client.key --debug
    ```

## Next Steps

Setting your environment depending on which language you want, and then get started!

 * **.NET** 

    Install [.NET 8](https://learn.microsoft.com/dotnet/core/install/linux) and then head to the [.NET SDK ](/dotnet/)

* **Go** 

    Install [Go](https://go.dev/doc/install) and then head to the [Go SDK](/go/)

* **Rust** 

    Install [Rust](https://www.rust-lang.org/tools/install) and then head to the [Rust SDK](/rust/)
