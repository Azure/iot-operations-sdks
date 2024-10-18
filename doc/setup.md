# Setup

The following instructions provide details on configuring your Kubernetes cluster 

## Anatomy of installer

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

## Setup your Language

### .NET

1. Install the [.NET 8 SDK](https://learn.microsoft.com/dotnet/core/install/linux)

1. Refer to the [SDK .NET documentation](/dotnet/) for further instructions on getting started.

### Go

1. Install Go by following the [Go Install Dev Doc](https://go.dev/doc/install).

1. Refer to the [SDK Go documentation](/go/) for further instructions on getting started.

### Rust

1. Install Rust by following [Installing Rust](https://www.rust-lang.org/tools/install).

1. Refer to the [Rust Go documentation](/rust/) for further instructions on getting started.
