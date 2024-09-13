rm -rf ./StateStoreGen
mkdir ./StateStoreGen
../../../../codegen/src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen --modelFile dss.json --lang csharp --outDir ./StateStoreGen
