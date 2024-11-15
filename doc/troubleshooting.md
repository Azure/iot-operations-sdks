# Troubleshooting

The following are issues that you may run into when interacting with this repository.

## `license-cla` stuck in pending state

*Problem*

Occasionally when creating a PR, the `license-cla` action will be stuck in a pending state. Usually an additional commit will force it to rerun, however you can also force a rerun without having to commit.

*Resolution*

Add the following comment to the PR as [described in the docs](https://github.com/microsoft/ContributorLicenseAgreement#re-running):

```
@microsoft-github-policy-service rerun
```