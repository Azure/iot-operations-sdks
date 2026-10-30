#!/usr/bin/env bash
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
input_root="${script_dir}/../../UA-Nodeset"
output_dir="${script_dir}/../../smd/models"
integrate=false
inherit_vars=false
include_tds=false
include_non_published=false

usage() {
    cat <<EOF
Usage: $(basename "$0") [options]

Options:
  --input-root <path>   Root directory containing recursive *.NodeSet2.xml inputs
  --output-dir <path>   Directory that receives generated *.TM.json files
  --integrate           Make each output file self-contained
  --inherit-vars        Add dov:includeInherited to applicable root forms
  --include-tds         Include Thing Descriptions in generated collections
  --include-non-published
                        Include UA-Nodeset demo, test, and example-only models
  --help                Show this help
EOF
}

while (($# > 0)); do
    case "$1" in
        --input-root)
            [[ $# -ge 2 ]] || { echo "Missing value for --input-root." >&2; exit 2; }
            input_root="$2"
            shift 2
            ;;
        --output-dir)
            [[ $# -ge 2 ]] || { echo "Missing value for --output-dir." >&2; exit 2; }
            output_dir="$2"
            shift 2
            ;;
        --integrate)
            integrate=true
            shift
            ;;
        --inherit-vars)
            inherit_vars=true
            shift
            ;;
        --include-tds)
            include_tds=true
            shift
            ;;
        --include-non-published)
            include_non_published=true
            shift
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

command -v dotnet >/dev/null 2>&1 || {
    echo "The .NET SDK is required, but 'dotnet' was not found on PATH." >&2
    exit 1
}

[[ -d "$input_root" ]] || {
    echo "The NodeSet input directory does not exist: $input_root" >&2
    exit 1
}

input_root="$(cd "$input_root" && pwd)"
output_parent="$(dirname "$output_dir")"
[[ -d "$output_parent" ]] || {
    echo "The output parent directory does not exist: $output_parent" >&2
    exit 1
}
output_dir="$(cd "$output_parent" && pwd)/$(basename "$output_dir")"

project_path="${script_dir}/src/Azure.Iot.Operations.Opc2Wot/Azure.Iot.Operations.Opc2Wot.csproj"
node_set_files=()
while IFS= read -r node_set_file; do
    if ! $include_non_published; then
        relative_path="${node_set_file#"${input_root}/"}"
        case "$relative_path" in
            DemoModel/DemoModel.NodeSet2.xml|LaserSystems/LaserSystem-Example.NodeSet2.xml|TestModel/TestModel.NodeSet2.xml)
                continue
                ;;
        esac
    fi
    node_set_files+=("$node_set_file")
done < <(find "$input_root" -type f -iname '*.NodeSet2.xml' -print | LC_ALL=C sort)

((${#node_set_files[@]} > 0)) || {
    echo "No *.NodeSet2.xml files were found below: $input_root" >&2
    exit 1
}

arguments=(
    run
    --project "$project_path"
    --configuration Debug
    --
    --nodeSets "${node_set_files[@]}"
    --outDir "$output_dir"
)

$integrate && arguments+=(--integrate)
$inherit_vars && arguments+=(--inheritVars)
$include_tds && arguments+=(--includeTDs)

echo "Generating WoT Thing Models from '$input_root' into '$output_dir'."
dotnet "${arguments[@]}"
