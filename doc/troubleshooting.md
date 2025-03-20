# Troubleshooting

The following are issues that you may run into when interacting with this repository.

**Contents:**
* [`license-cla` stuck in pending state](#license-cla-stuck-in-pending-state)
* [Codespaces error after resuming](#codespaces-error-after-resuming)
* [Azure IoT Operations with WSL](#azure-iot-operations-with-wsl)


## `license-cla` stuck in pending state

<ins>Problem</ins>

Occasionally when creating a PR, the `license-cla` action will be stuck in a pending state. Usually an additional commit will force it to rerun, however you can also force a rerun without having to commit.

<ins>Resolution</ins>

Add the following comment to the PR as [described in the docs](https://github.com/microsoft/ContributorLicenseAgreement#re-running):

```
@microsoft-github-policy-service rerun
```

## Codespaces error after resuming

<ins>Problem</ins>

Currently there is an issue when deploying Azure IoT Operations in Codespaces, that can result in a corrupt container when stopping and starting the codespace.

<ins>Resolution</ins>

We are working with the GitHub team to resolve this issue.

As a workaround, we recommend you deploy the devcontainer to your local machine or deploy directly on your own instance of Linux as outlined in the [setup instructions](/doc/setup.md).

## Azure IoT Operations with WSL

<ins>Problem</ins>

When installing Azure IoT Operations on WSL, the installation fails.

<ins>Resolution</ins>

A features is missing from the current WSL kernel (v5.15) that is needed for Azure IoT Operations to install. We are working with the WSL team on an upcoming update to kernel v6.6 which will resolve this issue.

As a workaround, we recommend deploying Azure IoT Operations directly on Linux, or installing a previous WSL package that contains the v6.6 kernel as outlined in the [setup instructions](/doc/setup.md).