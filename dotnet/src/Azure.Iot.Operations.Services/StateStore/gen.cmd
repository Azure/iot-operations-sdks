set codegen="..\..\..\..\..\tools\codegen\src\Akri.Dtdl.Codegen\bin\Debug\net8.0\Akri.Dtdl.Codegen.exe"
%codegen% --modelFile dss.json --lang csharp --outDir %TEMP%\Azure.Iot.Operations.Services.StateStore.Gen
rmdir StateStoreGen /q /s
mkdir StateStoreGen
copy /y %TEMP%\Azure.Iot.Operations.Services.StateStore.Gen\dtmi_ms_aio_mq_StateStore__1\*.cs StateStoreGen\
copy /y %TEMP%\Azure.Iot.Operations.Services.StateStore.Gen\PassthroughSerializer.cs StateStoreGen\PassthroughSerializer.cs
del /s /q %TEMP%\Azure.Iot.Operations.Services.StateStore.Gen