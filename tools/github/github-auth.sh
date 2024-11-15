#!/bin/sh

# Reauthenticate with GitHub so that we can push to other organizations

unset GITHUB_TOKEN
gh auth login
gh auth setup-git
