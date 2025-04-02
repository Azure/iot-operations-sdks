# Developing with GitHub Codespaces

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

## Adding secret to allow CI workflows to run on a fork

Repository secrets are not passed to a fork, so the CI workflows will fail when run from the fork. The solution is to create the secret in your forked repository which the CI workflows will access when executing.

If you create a PR from a branch of the main repository, no change is needed as the repository already contains the required secret.

### Create a token

1. Browse to [Fine-grained tokens](https://github.com/settings/personal-access-tokens)
1. Click `Generate new token`
1. Set the following:
   * **Resource owner** - Azure
   * **Expiration** - 180 days
   * **Repository access** - only select repositories
   * **Select repositories** - Azure/iot-operations-sdks-action
   * **Repository permissions** - Contents: Read-only
1. Create the token, and make a copy of the secret for next section

### Create secret

1. Go to your iot-operations-sdks fork
1. Open **Settings**
1. Go to **Secrets and variables > Actions**
1. Click **New repository secret**
1. Add the following details:
   * **Name** - `AIO_SDKS_ACTION_TOKEN` 
   * **Secret** - The contents of the secret from the previous section
1. Click **Add secret**

You can now run an iot-operations sdks workflow successfully from your repository fork.