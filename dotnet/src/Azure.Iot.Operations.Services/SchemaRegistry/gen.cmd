set codegen="..\..\..\..\..\tools\codegen\src\Azure.Iot.Operations.ProtocolCompiler\bin\Debug\net8.0\Azure.Iot.Operations.ProtocolCompiler.exe"
%codegen% --modelFile SchemaRegistry-1.json --lang csharp --outDir %TEMP%\Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry
copy /y %TEMP%\Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry\dtmi_ms_adr_SchemaRegistry__1\*.cs dtmi_ms_adr_SchemaRegistry__1
del /s /q %TEMP%\Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry