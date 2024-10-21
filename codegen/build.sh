#!/bin/sh
# This script is used to build the Protocol Compiler in CI builds and can be
# used on the Linux command line (with dotnet installed). It is not necessary
# for building with Visual Studio.

NAMESPACE=Azure.Iot.Operations.ProtocolCompiler
SCRIPTDIR=$(dirname "$0")
SOURCEDIR=$SCRIPTDIR/src/$NAMESPACE

if ! which t4 > /dev/null; then
    dotnet tool install -g dotnet-t4
fi

find "$SOURCEDIR" -name '*.tt' | while IFS= read -r f; do
    t4 --class=$NAMESPACE.$(basename "$f" .tt) "$f"
done

if [ "$1" = "test" ]; then
    shift
    dotnet build "$@" "$SCRIPTDIR"
    dotnet test "$SCRIPTDIR"
else
    dotnet build "$@" "$SOURCEDIR/$NAMESPACE.csproj"
fi
