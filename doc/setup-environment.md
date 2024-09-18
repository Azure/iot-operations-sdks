# Environment Setup

## Platform

### **Codespaces**

    Use Github Codespaces to try the Azure IoT Operations SDKs on a Kubernetes cluster without installing anything on your local machine. Setting up in [GitHub Codespaces](https://github.com/features/codespaces) can be done with the below badge:

    [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Azure/iot-operations-sdks?hide_repo_select=true&editor=vscode)


### **Linux**

    Install on Linux by following the [k3d documentation](https://k3d.io/v5.7.4/#releases).

### **Windows (WSL)**

    Installation on Windows uses WSL, which can be added by [Installing Linux on Windows with WSL](https://learn.microsoft.com/en-us/windows/wsl/install).
    Ensure you also follow the steps under **Upgrade version from WSL 1 to WSL 2**.
    Then [install k3d](https://k3d.io/v5.7.4/#releases).

## Cluster Setup

Your Kubernetes cluster and Azure IoT Operations can be setup via Helm or via Azure Arc. Steps for both are included below.

### Helm (Nightly Build)

1. [Install Helm](https://helm.sh/docs/intro/install/)

2. Create a cluster with the `initialize-cluster` script:

    From the root directory of the repo:
    ```bash
    ./tools/deployment/initialize_cluster.sh
    ```

3. Install Azure IoT Operations with the `deploy_aio.sh` script:

    From the root directory of the repo, for the **nightly** build
    ```bash
    ./tools/deployment/deploy-aio.sh nightly
    ```

Scripts can be executed with the above commands for ease of use, however if you would like to see the exact steps being performed or would like more info, navigate to the [deployment folder](../tools/deployment/).

### Azure Arc (Release Build)

The release build must be installed using Azure Arc.

1. Run the initialize cluster script

    From the main repo directory:
    ```bash
    ./tools/deployment/initialize_cluster.sh
    ```

2. [Arc-enable your cluster](https://learn.microsoft.com/azure/iot-operations/deploy-iot-ops/howto-prepare-cluster?tabs=ubuntu#arc-enable-your-cluster)
3. [Install IoT Operations](https://learn.microsoft.com/azure/iot-operations/deploy-iot-ops/howto-deploy-iot-operations?tabs=cli)

## Language

### .NET

1. Install the .NET SDK by following [(Install .NET on Linux)](https://learn.microsoft.com/dotnet/core/install/linux).

2. Refer to the [.NET documentation](/dotnet/) for further steps.

### Go

1. Install Go [(steps here)](https://go.dev/doc/install).

2. Refer to the [Go sample documentation](/go/samples/README.md)

### Rust

1. Refer to the [Rust documentation](/rust/) for further steps.