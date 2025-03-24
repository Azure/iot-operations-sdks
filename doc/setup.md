# Setup

The following instructions will setup up a development environment for building and running the samples, as well as creating and testing your own Azure IoT Operations edge applications.

**Contents:**
* [Setup the environment](#setup-the-environment)
* [Install Azure IoT Operations](#install-azure-iot-operations)
* [Shell configuration](#shell-configuration)
* [Testing the installation](#testing-the-installation)
* [Configuration summary](#configuration-summary)

## Setup the environment

The following development environment setup options utilize [k3d](https://k3d.io/#what-is-k3d) to simplify Kubernetes cluster creation. Codespaces provides the most streamlined experience and can get the development environment up and running in a couple of minutes.

Follow the steps in **one of the sections** below to get your development environment up and running:

* [Option 1 - **Codespaces**](#option-1---codespaces)
* [Option 2 - **Linux**](#option-2---linux)
* [Option 3 - **Linux devcontainer on Windows**](#option-3---linux-devcontainer-on-windows)
* [Option 4 - **Windows Subsystem for Linux**](#option-4---windows-subsystem-for-linux)

### Option 1 - Codespaces

> [!CAUTION]
> We are currently experiencing container corruption with Azure IoT Operations deployed in a codespace, so we don't recommend this path until we have resolved the issue with the GitHub team.

1. Create a **codespace** from the *Azure IoT Operations SDKs* repository by clicking the following button:

    [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Azure/iot-operations-sdks?quickstart=1&editor=vscode)

1. Once the codespace is created, you will have a container with the developer tools and a local k3s cluster pre-installed.

### Option 2 - Linux

1. Install [Ubuntu](https://ubuntu.com/download/desktop)

1. Install [Docker Engine](https://docs.docker.com/engine/install/ubuntu/)

1. Clone the *Azure IoT Operations SDKs* repository:

    ```bash
    git clone https://github.com/Azure/iot-operations-sdks
    ```

### Option 3 - Linux devcontainer on Windows

> [!WARNING]
> The latest WSL release **doesn't support Azure IoT Operations**. You will need to install [WSL v2.3.14](https://github.com/microsoft/WSL/releases/tag/2.3.14) which contain the required feature as outlined in the steps below.

1. Install [WSL v2.3.14](https://github.com/microsoft/WSL/releases/tag/2.3.14) (contains kernel v6.6)

1. Install [Docker Desktop for Windows](https://docs.docker.com/desktop/features/wsl/)  with WSL 2 backend

1. Install [VS Code](https://code.visualstudio.com/) and the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)

1. Launch VS Code, and clone the repository in a container:

    `F1 > Dev Containers: Clone Repository in Container Volume...`

1. When prompted, enter the *Azure IoT Operations SDKs* URL:

    ```
    https://github.com/azure/iot-operations-sdks
    ```

> [!TIP]
> To reconnect to the container in VSCode, choose `F1 > Dev Containers: Attach to Running Container...` and then select the container name created previously.

### Option 4 - Windows Subsystem for Linux

> [!WARNING]
> The latest WSL release **doesn't support Azure IoT Operations**. You will need to install [WSL v2.3.14](https://github.com/microsoft/WSL/releases/tag/2.3.14) which contain the required feature as outlined in the steps below.

1. Install [WSL v2.3.14](https://github.com/microsoft/WSL/releases/tag/2.3.14) (contains kernel v6.6)

1. Install [Docker Desktop for Windows](https://docs.docker.com/desktop/features/wsl/) with WSL 2 backend

1. Clone the *Azure IoT Operations SDKs* repository:

    ```bash
    git clone https://github.com/Azure/iot-operations-sdks
    ```

## Install Azure IoT Operations

Azure IoT Operations will be installed to the development cluster, and then the configuration will be altered to provide additional off-cluster access methods to streamline development:

1. Launch a shell, and change to the root directory of the *Azure IoT Operations SDKs* repository.

1. If required, initialize the cluster and install required dependencies:

    ```bash
    sudo ./tools/deployment/initialize-cluster.sh
    ```

1. Follow the [Azure IoT Operations documentation](https://learn.microsoft.com/azure/iot-operations/get-started-end-to-end-sample/quickstart-deploy?tabs=codespaces#connect-cluster-to-azure-arc) to connect Azure Arc and deploy Azure IoT Operations.

1. Check that Azure IoT Operations is successfully installed and **Resolve any errors before continuing**:

    ```bash
    az iot ops check
    ```

    Expected output:

    ```
    ╭─────── Check Summary ───────╮
    │ 13 check(s) succeeded.      │
    │ 0 check(s) raised warnings. │
    │ 0 check(s) raised errors.   │
    │ 4 check(s) were skipped.    │
    ╰─────────────────────────────╯
    ```

1. Run the `configure-aio` script to configure Azure IoT Operations for development:

    ```bash
    ./tools/deployment/configure-aio.sh
    ```

## Shell configuration

The samples within this repository read configuration from environment variables. We have provided a [.env](/.env) file in the repository root that exports the variables used by the samples to connect to the MQTT Broker.

```bash
source <REPOSITORY ROOT>/.env
```

## Testing the installation

To test the setup is working correctly, use `mosquitto_pub` to connect to the MQTT broker to validate the x509 certs, SAT and trust bundle.

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

## Configuration summary

### MQTT broker configuration

 With the installation complete, the cluster will contain the following MQTT broker definitions:

| Component Type | Name | Description
|-|-|-|
| `Broker` | default | The MQTT broker |
| `BrokerListener` | default | Provides **cluster access** to the MQTT Broker |
| `BrokerListener` | default-external | Provides **off-cluster access** to the MQTT Broker |
| `BrokerAuthentication` | default | SAT authentication definition
| `BrokerAuthentication` | default-x509 | An x509 authentication definition

### MQTT broker access

The MQTT broker can be accessed both on-cluster and off-cluster using the connection information below. Refer to [ Connection Settings](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/connection-settings.md) for information on which environment variables to use when configuration your application.

> [!NOTE]
>
> The hostname when accessing the MQTT broker off-cluster may differ from `localhost` depending on your setup.

| Hostname | Authentication | TLS | On cluster port | Off cluster port |
|-|-|-|-|-|
| `aio-broker` | SAT | :white_check_mark: | `18883` | - |
| `localhost` | None | :x: | `1883` | `1883` |
| `localhost` | x509 | :white_check_mark: | `8883` | `8883` |
| `localhost` | SAT | :white_check_mark: | `8884` | `8884` |

### Development artifacts

As part of the deployment script, the following files are created in the local environment, to facilitate connection and authentication to the MQTT broker. These files are located in the `.session` directory, found at the repository root.

| File | Description |
|-|-|
| `broker-ca.crt` | The MQTT broker trust bundle required to validate the MQTT broker on ports `8883` and `8884`
| `token.txt` | A Service authentication token (SAT) for authenticating with the MQTT broker on `8884`
| `client.crt` | A x509 client certificate for authenticating with the MQTT broker on port `8883`
| `client.key` | A x509 client private key for authenticating with the MQTT broker on port `8883`

## Next Steps

The development environment is now setup! Refer to the language documentation for further instructions on setting up the SDK:

* Get started with the [.NET SDK ](/dotnet/)

* Get started with the [Go SDK](/go/)

* Get started with the [Rust SDK](/rust/)
