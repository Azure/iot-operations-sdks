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

        public static void GenerateType(string language, string projectName, string schemaFileName, DirectoryInfo workingDir, string genRoot, string genNamespace, HashSet<string> sourceFilePaths, HashSet<SchemaKind> distinctSchemaKinds)
        {
            string schemaFileFolder = Path.Combine(workingDir.FullName, genNamespace);
            string schemaFilePath = Path.Combine(schemaFileFolder, schemaFileName);
            string schemaIncludeFolder = Path.Combine(workingDir.FullName, ResourceNames.IncludeFolder);

            if (!Directory.Exists(genRoot))
            {
                Directory.CreateDirectory(genRoot);
            }

            if (schemaFileName.EndsWith(".proto"))
            {
                try
                {
                    Process.Start("protoc", $"--{language}_out={Path.Combine(genRoot, genNamespace)} --proto_path={schemaFileFolder} --proto_path={schemaIncludeFolder} {schemaFileName}");
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
                    typeGenerator.GenerateTypeFromSchema(projectName, genNamespace, schemaType, schemaStandardizer.SerializationFormat, genRoot, sourceFilePaths);
                }
            }
            else
            {
                throw new Exception($"Unable to process schema file \"{schemaFilePath}\" because file extension is not recognized");
            }
        }
    }
}
