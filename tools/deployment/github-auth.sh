#!/bin/sh

# Reauthenticate with GitHub so that we can push to other organisations

unset GITHUB_TOKEN
gh auth login
gh auth setup-git
