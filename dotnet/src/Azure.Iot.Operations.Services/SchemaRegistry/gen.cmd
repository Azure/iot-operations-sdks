set codegen="..\..\..\..\..\tools\codegen\src\Azure.Iot.Operations.ProtocolCompiler\bin\Debug\net8.0\Azure.Iot.Operations.ProtocolCompiler.exe"
%codegen% --modelFile ../../../../eng/dtdl/SchemaRegistry-1.json --lang csharp --outDir %TEMP%\Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry
copy /y %TEMP%\Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry\SchemaRegistry\*.cs SchemaRegistry
del /s /q %TEMP%\Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry