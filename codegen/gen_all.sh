#!/bin/bash

dotnet build -c Debug src/Akri.Dtdl.Codegen/Akri.Dtdl.Codegen.csproj 

pushd /; dotnet tool install --global Apache.Avro.Tools; popd

gen_scripts=$(find ./../../ -type f -name "gen.sh")

for script in $gen_scripts; 
do
    relativepath=$(dirname $script)
    script_path=$(realpath $relativepath)
    echo "Running $script_path gen.sh \n"
    pushd $script_path
    bash gen.sh
    popd
done