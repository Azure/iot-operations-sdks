This github action step requires special authentication to access a private repo during a Github action run. To do this, we use SSH Keys (as described [here](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/managing-deploy-keys#deploy-keys))

Any generated SSH key will be valid in perpetuity and should not need renewal/rotation, but in case it does for any reason, here are the steps for setting this all up again.

1) Generate an SSH key on your local machine following [these instructions](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/generating-a-new-ssh-key-and-adding-it-to-the-ssh-agent#generating-a-new-ssh-key). It should give you two output files. One is the private key, the other is the public key
2) In the [actions repo](https://github.com/Azure/iot-operations-sdks-action), navigate to the settings tab -> deploy keys. In that page, hit "Add deploy key" and paste the contents of the public key from the previous step
3) In this repo, navigate to the settings tab -> secrets and variables -> Actions and create a new repository secret. Name it something like "ACTIONS_REPO_SSH_KEY" and paste the contents of the private key into its value
4) Make sure that the Github action step in this folder is configured to use this SSH key to checkout the private repo like

```yaml
    # This build step that checks out the private repo using the ssh key
    - name: Checkout deploy action
      uses: actions/checkout@v4
      with:
        repository: azure/iot-operations-sdks-action
        path: action-deploy
        ssh-key: ${{ inputs.ssh-key }}
```

```yaml
    # The pipeline that calls this build step
    - name: Install AIO
      uses: ./.github/actions/configure-aio
      with:
        wait-for-broker: 'true'
        install-dotnet: 'true'
        ssh-key: ${{ secrets.ACTIONS_REPO_SSH_KEY }}
```