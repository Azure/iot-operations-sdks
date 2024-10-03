dotnet build ..\..\..\tools\codegen\src\Azure.Iot.Operations.ProtocolCompiler\Azure.Iot.Operations.ProtocolCompiler.csproj

set gen=..\..\..\tools\codegen\src\Azure.Iot.Operations.ProtocolCompiler\bin\Debug\net8.0\Azure.Iot.Operations.ProtocolCompiler.exe

%gen% --modelFile counter.json --outDir . --sdkPath ..\..\src\Azure.Iot.Operations.Protocol
%gen% --modelFile math.json --outDir . --sdkPath ..\..\src\Azure.Iot.Operations.Protocol
%gen% --modelFile memmon.json --outDir . --sdkPath ..\..\src\Azure.Iot.Operations.Protocol
%gen% --modelFile passthrough.json --outDir . --sdkPath ..\..\src\Azure.Iot.Operations.Protocol

dotnet build
