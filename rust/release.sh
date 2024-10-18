#!/bin/sh

# export PAT=xxx

set -o errexit # fail if any command fails

script_dir=$(dirname $(readlink -f $0))
cd $script_dir

echo -n Basic $(echo -n PAT:$PAT | base64) | cargo login --registry aio-sdks
cargo publish --manifest-path ./azure_iot_operations_mqtt/Cargo.toml --registry aio-sdks
cargo publish --manifest-path ./azure_iot_operations_protocol/Cargo.toml --registry aio-sdks
cargo publish --manifest-path ./azure_iot_operations_services/Cargo.toml --registry aio-sdks
