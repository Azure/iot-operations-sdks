#!/bin/bash

for tag in $(gh api repos/Azure/iot-operations-sdks/releases -q '.[].tag_name'); do
    if [[ $tag =~ (.*)/(.*)/(.*) ]]; then
      lang=${BASH_REMATCH[1]}
      package=${BASH_REMATCH[2]}
      version=${BASH_REMATCH[3]}

      if [[ $lang == "rust" && -z ${rust_tag} ]]; then
        rust_tag=$tag
      elif [[ $lang == "go" && -z ${go_tag} ]]; then
        go_tag=$tag
      elif [[ $lang == "dotnet" && -z ${dotnet_tag} ]]; then
        dotnet_tag=$tag
      fi
    else
      echo "Unknown tag: $tag"
    fi
done

echo "rust_tag=$rust_tag" >> $GITHUB_OUTPUT
echo "go_tag=$go_tag" >> $GITHUB_OUTPUT
echo "dotnet_tag=$dotnet_tag" >> $GITHUB_OUTPUT
