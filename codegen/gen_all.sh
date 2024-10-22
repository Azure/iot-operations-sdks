#!/bin/bash
SCRIPTDIR=$(dirname "$0")

"$SCRIPTDIR/build.sh"

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
