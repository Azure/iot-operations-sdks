set codegen="..\..\..\..\..\tools\codegen\src\Azure.Iot.Operations.ProtocolCompiler\bin\Debug\net8.0\Azure.Iot.Operations.ProtocolCompiler.exe"
%codegen% --modelFile discovered_resources_commands_v1.json --lang csharp --outDir %TEMP%\Azure.Iot.Operations.Services.Akri.Akri
copy /y %TEMP%\Azure.Iot.Operations.Services.Akri.Akri\dtmi_ms_adr_Akri__1\*.cs dtmi_ms_adr_Akri__1
del /s /q %TEMP%\Azure.Iot.Operations.Services.Akri.Akri
