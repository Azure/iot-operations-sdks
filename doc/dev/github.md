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
