set codegen="..\..\..\..\..\tools\codegen\src\Azure.Iot.Operations.ProtocolCompiler\bin\Debug\net8.0\Azure.Iot.Operations.ProtocolCompiler.exe"
%codegen% --modelFile dss.json --lang csharp --outDir %TEMP%\Azure.Iot.Operations.Services.StateStore.Gen
rmdir StateStoreGen /q /s
mkdir StateStoreGen
copy /y %TEMP%\Azure.Iot.Operations.Services.StateStore.Gen\StateStore\*.cs StateStoreGen\
copy /y %TEMP%\Azure.Iot.Operations.Services.StateStore.Gen\PassthroughSerializer.cs StateStoreGen\PassthroughSerializer.cs
del /s /q %TEMP%\Azure.Iot.Operations.Services.StateStore.Gen