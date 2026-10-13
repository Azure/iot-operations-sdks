REM Counter, CustomTopicTokens, and Passthrough are generated from WoT Thing Models.
REM Math (Protobuf) and Memmon (Avro) are not supported by the WoT protocol compiler and are no longer regenerated.
dotnet build -c Debug ..\..\..\..\wot-codegen\src\Azure.Iot.Operations.ProtocolCompiler\Azure.Iot.Operations.ProtocolCompiler.csproj

set gen=..\..\..\..\wot-codegen\src\Azure.Iot.Operations.ProtocolCompiler\bin\Debug\net10.0\Azure.Iot.Operations.ProtocolCompiler.exe
set sdk=..\..\..\src\Azure.Iot.Operations.Protocol

rmdir /s /q Counter CustomTopicTokens Passthrough schemas 2>nul

%gen% --things ..\..\..\..\eng\test\schema-samples\Counter.TM.json --outDir . --sdkPath %sdk% --namespace Counter --lang csharp --noProj --workingDir schemas/Counter
%gen% --things ..\..\..\..\eng\test\schema-samples\CustomTopicTokens.TM.json --outDir . --sdkPath %sdk% --namespace CustomTopicTokens --lang csharp --noProj --workingDir schemas/CustomTopicTokens
%gen% --things Passthrough.TM.json --outDir . --sdkPath %sdk% --namespace Passthrough --lang csharp --noProj --workingDir schemas/Passthrough

dotnet build
