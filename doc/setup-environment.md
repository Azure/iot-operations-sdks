# Environment Setup

## Platform
1. Linux - Codespaces

Use Github Codespaces to try the Azure IoT Operations SDKs on a Kubernetes cluster without installing anything on your local machine. Setting up in [GitHub Codespaces](https://github.com/features/codespaces) can be done with the below badge:

 [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Azure/iot-operations-sdks?hide_repo_select=true&editor=vscode)


1. Linux - Native

Installation in Native Linux can be accomplished by [installing the latest verison of k3d](https://k3d.io/v5.7.4/#releases).

1. Linux - WSL

Installation of WSL can be accomplished by [following this doc](https://learn.microsoft.com/en-us/windows/wsl/install).
Ensure you also follow the steps under **Upgrade version from WSL 1 to WSL 2**.

## Cluster Setup
Your Kubernetes cluster and AIO can be setup via Helm or via Azure Arc. Steps for both are included below.

### Helm
1. Install helm
```bash
sudo snap install helm --classic
```

1. Create a cluster with the `initialize-cluster` script:
From the main repo directory:
```bash
./tools/deployment/initialize_cluster.sh
```

1. Install the AIO build with the `deploy_aio.sh` script:

From the main repo directory, for the **nightly** build
```bash
./tools/deployment/deploy-aio.sh nightly
```

From the main repo directory, for the **release** build
```bash
./tools/deployment/deploy-aio.sh release
```

Scripts can be executed with the above commands for ease of use, however if you would like to see the exact steps being performed or would like more info, navigate to the [deployment folder](../tools/deployment/).

### Arc + IoT Operations
1. [Install Arc + IoT Operations](https://learn.microsoft.com/en-us/azure/iot-operations/get-started-end-to-end-sample/quickstart-deploy)
1. [Prepare your Kubernetes cluster](https://learn.microsoft.com/en-us/azure/iot-operations/deploy-iot-ops/howto-prepare-cluster?tabs=ubuntu)

## Language
### dotnet
1. Install the dotnet SDK [(steps here)](https://learn.microsoft.com/en-us/dotnet/core/install/linux-snap-sdk).

```bash
sudo snap install dotnet-sdk --classic
```

1. Refer to the [.NET Packaging guide](../dotnet/README.md#packaging) to install desired packages

1. Navigate to the folder of the sample you want to run
Build the sample:
```bash
dotnet build
```

Run the sample:
```bash
dotnet run
```

### go
1. Install Go
```bash
wget https://dl.google.com/go/go1.23.1.src.tar.gz
sudo tar -xvf go1.18.3.linux-amd64.tar.gz
sudo mv go /usr/local
```

1. Refer to the [Go sample documentation](../go/samples/README.md)

### rust

## Resetting cluster
If you need to reset your cluster:
```bash
helm un broker
```

Then go back through the steps you followed in **Cluster Setup**.