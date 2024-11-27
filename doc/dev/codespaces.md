# Developing with GitHub Codespaces

## VSCode to Codespaces

Opening the codespace in Desktop VSCode will allow the MQTT broker to be accessed locally via the port forwarding function.

1. Run VSCode.
1. Press `F1` and choose `Codespaces: Connect to Codespace...`.
1. Select the codespace for this repository you created earlier.

> [!IMPORTANT]
> The `az login` command may fail when used in the **Codespaces web browser**. Run the Codespaces in a local VSCode to bypass this issue.

## Creating new k3d cluster

A k3d cluster is already installed in Codespaces, however you can add another cluster if desired using the following:

1. Install k3d (if needed):

    ```bash
    curl -s https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash
    ```

1. Stop the existing cluster:

    The clusters both expose the same ports (1883 and 8883), so they can't run simultaneously. Also the codeSpace has limited RAM so running multiple clusters may fail, so its best to stop the other clusters:

    ```bash
    k3d cluster stop
    k3d cluster list
    ```

    output:
    ```output
    NAME     SERVERS   AGENTS   LOADBALANCER
    default  0/1       0/0      true
    ```

1. Create a new cluster with ports forwarded:

    ```bash
    k3d cluster create mqttbroker \
        -p '1883:31883@loadbalancer' \
        -p '8883:38883@loadbalancer' \
        -p '8884:38884@loadbalancer'
    ```

1. Set the default context / namespace if desired:

    ```bash
    kubectl config set-context k3d-k3s-default --namespace=azure-iot-operations
    ```

## K3d image importing

Instead of using a container registry, you can inject an image directly into the cluster using k3d:

```bash
k3d image import {imageName}
```

And using the Yaml, setting the pull policy to `Never`:
```yaml
- spec:
  - name: {containerName}
    image: {imageName}
    imagePullPolicy: Never
```

## K3d container registry

A container registry is available as `registry.localhost:5000` from the local machine, as is accessible as `registry:5000` from the cluster.

1. List repositories (image) in the registry:

    ```bash
    curl -X GET http://registry.localhost:5000/v2/_catalog
    ```

1. List tags for a repository:

    ```bash
    curl -X GET http://registry.localhost:5000/v2/{repository}/tags/list
    ```

## Reauthenticate Git

By default, Codespaces doesn't allow access to other private repositories, especially outside the GitHub Organization. To access these repositories you will need to regenerate your token with the following:

1. Clear the current GitHub token:

    ```bash
    unset GITHUB_TOKEN
    ```

1. Log into GitHub using the default options, and then setup the Git authentication:

    ```bash
    gh auth login
    gh auth setup-git
    ```

## Developing on a fork

By default, if you create a Codespace on a fork, you will quickly run out of free quota as it will be billed to your individual organization. The workaround is to create the Codespace in the Azure org, and then switch the origin to the fork.

1. [Reauthenticate Git](#reauthenticate-git).

1. Create a [Personal Access Token](https://github.com/settings/tokens) (PAT) with write access to the repository fork.

1. Add the `GH_TOKEN` variable in your [Codespaces user secrets](https://github.com/settings/codespaces) to the PAT.

1. Restart the codespace to pull in the new `GH_TOKEN`.

1. Rename the origin to upstream, and add your fork as the new origin:

    ```bash
    git remote rename origin upstream
    git remote set-url origin https://gitlab.com/<forked-org>/azure-iot-operations-sdks
    ```

1. Update the local files:

   ```bash
   git fetch origin
   git pull origin main
   ```

## Using K9s

K9s comes pre-installed in this CodeSpace and provides easy access to viewing details about the cluster.

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

1. Open a shell in the k3d container:

    ```bash
    docker exec -it k3d-k3s-default-server-0 sh
    ```

1. List the images:

    ```bash
    crictl images
    ```
