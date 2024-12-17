namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;

    internal static class TypesGenerator
    {
        static readonly Dictionary<string, ISchemaStandardizer> SchemaStandardizers = new()
        {
            { ".schema.json", new JsonSchemaStandardizer() },
            { ".avsc", new AvroSchemaStandardizer() },
        };

        static readonly Dictionary<string, ITypeGenerator> TypeGenerators = new()
        {
            { "csharp", new DotNetTypeGenerator() },
            { "go", new GoTypeGenerator() },
            { "rust", new RustTypeGenerator() },
        };

        public static void GenerateType(string language, string projectName, string schemaFileName, DirectoryInfo workingDir, DirectoryInfo outDir, string genNamespace, HashSet<string> sourceFilePaths, HashSet<SchemaKind> distinctSchemaKinds)
        {
            string schemaFileFolder = Path.Combine(workingDir.FullName, genNamespace);
            string schemaFilePath = Path.Combine(schemaFileFolder, schemaFileName);
            string schemaIncludeFolder = Path.Combine(workingDir.FullName, ResourceNames.IncludeFolder);

            if (!outDir.Exists)
            {
                outDir.Create();
            }

            if (schemaFileName.EndsWith(".avsc") && language == "csharp")
            {
                try
                {
                    Process.Start("avrogen", $"-s {schemaFilePath} {outDir.Parent!.FullName}");
                }
                catch (Win32Exception)
                {
                    Console.WriteLine("avrogen tool not found; install via command:");
                    Console.WriteLine("  dotnet tool install --global Apache.Avro.Tools");
                    Environment.Exit(1);
                }
            }
            else if (schemaFileName.EndsWith(".proto"))
            {
                try
                {
                    Process.Start("protoc", $"--{language}_out={Path.Combine(outDir.FullName, genNamespace)} --proto_path={schemaFileFolder} --proto_path={schemaIncludeFolder} {schemaFileName}");
                }
                catch (Win32Exception)
                {
                    Console.WriteLine("protoc tool not found; install per instructions: https://github.com/protocolbuffers/protobuf/releases/latest");
                    Environment.Exit(1);
                }
            }
            else if (SchemaStandardizers.Any(ss => schemaFileName.EndsWith(ss.Key)))
            {
                ITypeGenerator typeGenerator = TypeGenerators[language];
                ISchemaStandardizer schemaStandardizer = SchemaStandardizers.First(ss => schemaFileName.EndsWith(ss.Key)).Value;

                foreach (SchemaType schemaType in schemaStandardizer.GetStandardizedSchemas(schemaFilePath))
                {
                    distinctSchemaKinds.Add(schemaType.Kind);
                    typeGenerator.GenerateTypeFromSchema(projectName, genNamespace, schemaType, outDir.FullName, sourceFilePaths);
                }
            }
            else
            {
                throw new Exception($"Unable to process schema file \"{schemaFilePath}\" because file extension is not recognized");
            }
        }
    }
}
