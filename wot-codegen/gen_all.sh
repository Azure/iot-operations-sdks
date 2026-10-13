#!/bin/bash
SCRIPTDIR=$(dirname "$0")
REPOROOT=$(realpath "$SCRIPTDIR/..")

dotnet build -c Debug "$SCRIPTDIR/src/Azure.Iot.Operations.ProtocolCompiler/Azure.Iot.Operations.ProtocolCompiler.csproj"

# Only run gen.sh scripts that use the WoT protocol compiler; DTDL-based scripts are skipped.
for script in $(grep -rl --include=gen.sh "wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler" "$REPOROOT"); do
    script_path=$(realpath $(dirname $script))
    echo "Running $script_path gen.sh"
    pushd $script_path
    bash gen.sh
    popd
done
