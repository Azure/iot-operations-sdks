dotnet build ..\..\..\tools\codegen\src\Akri.Dtdl.Codegen\Akri.Dtdl.Codegen.csproj

set gen=..\..\..\tools\codegen\src\Akri.Dtdl.Codegen\bin\Debug\net8.0\Akri.Dtdl.Codegen.exe

%gen% --modelFile counter.json --outDir . --sdkPath ..\..\src\Azure.Iot.Operations.Protocol
%gen% --modelFile math.json --outDir . --sdkPath ..\..\src\Azure.Iot.Operations.Protocol
%gen% --modelFile memmon.json --outDir . --sdkPath ..\..\src\Azure.Iot.Operations.Protocol
%gen% --modelFile passthrough.json --outDir . --sdkPath ..\..\src\Azure.Iot.Operations.Protocol

dotnet build
