#!/bin/bash
SCRIPTDIR=$(dirname "$0")

dotnet build -c Debug src/Azure.Iot.Operations.ProtocolCompiler/Azure.Iot.Operations.ProtocolCompiler.csproj 

if ! which avrogen > /dev/null; then
    dotnet tool install --global Apache.Avro.Tools
fi

for script in $(find "$SCRIPTDIR/.." -name "gen.sh"); do
    script_path=$(realpath $(dirname $script))
    echo "Running $script_path gen.sh"
    pushd $script_path
    bash gen.sh
    popd
done
