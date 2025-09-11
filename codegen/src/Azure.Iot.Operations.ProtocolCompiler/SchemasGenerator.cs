namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Azure.Iot.Operations.ProtocolCompilerLib;
    using DTDLParser;
    using DTDLParser.Models;

    internal static class SchemasGenerator
    {
        public static bool GenerateSchemas(IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict, Dtmi interfaceId, int mqttVersion, string projectName, DirectoryInfo workingDir, CodeName genNamespace, CodeName? sharedPrefix)
        {
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[interfaceId];

            TopicCollisionDetector telemetryTopicCollisionDetector = TopicCollisionDetector.GetTelemetryTopicCollisionDetector();
            TopicCollisionDetector commandTopicCollisionDetector = TopicCollisionDetector.GetCommandTopicCollisionDetector();

            telemetryTopicCollisionDetector.Check(dtInterface, dtInterface.Telemetries.Keys, mqttVersion);
            commandTopicCollisionDetector.Check(dtInterface, dtInterface.Commands.Keys, mqttVersion);

            var schemaGenerator = new SchemaGenerator(modelDict, projectName, dtInterface, mqttVersion, genNamespace);

            Dictionary<string, int> schemaCounts = new();

            var schemaWriter = new SchemaWriter(workingDir.FullName, schemaCounts);

            schemaGenerator.GenerateInterfaceAnnex(schemaWriter.Accept, sharedPrefix);

            schemaGenerator.GenerateTelemetrySchemas(schemaWriter.Accept, sharedPrefix);
            schemaGenerator.GeneratePropertySchemas(schemaWriter.Accept, sharedPrefix);
            schemaGenerator.GenerateCommandSchemas(schemaWriter.Accept, sharedPrefix);
            schemaGenerator.GenerateObjects(schemaWriter.Accept, sharedPrefix);
            schemaGenerator.GenerateEnums(schemaWriter.Accept, sharedPrefix);
            schemaGenerator.GenerateJsonErrorFields(schemaWriter.Accept, sharedPrefix);
            schemaGenerator.GenerateArrays(schemaWriter.Accept);
            schemaGenerator.GenerateMaps(schemaWriter.Accept);
            schemaGenerator.CopyIncludedSchemas(schemaWriter.Accept);

            if (schemaCounts.Any(kv => kv.Value > 1))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Aborting schema generation due to duplicate generated names:");
                Console.ResetColor();
                foreach (KeyValuePair<string, int> schemaCount in schemaCounts.Where(kv => kv.Value > 1))
                {
                    Console.WriteLine($"  {schemaCount.Key}");
                }

                string exampleName = schemaCounts.FirstOrDefault(kv => kv.Value > 1 && kv.Key.EndsWith("Schema")).Key ?? "somethingSchema";
                string preName = exampleName.Substring(0, exampleName.Length - "Schema".Length);

                Console.WriteLine();
                Console.WriteLine(@"HINT: You can force a generated name by assigning an ""@id"" value, whose last label will determine the name, like this:");
                Console.WriteLine();
                Console.WriteLine($"    \"name\": \"{preName}\",");
                Console.WriteLine(@"    ""schema"": {");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(@"      ""@id"": ""dtmi:foo:bar:baz:SomeNameYouLike;1"",");
                Console.ResetColor();
                Console.WriteLine(@"      ""@type"": . . .");
                Console.WriteLine();

                Console.WriteLine(@"HINT: If your model contains a duplicated definition, you can outline it to the ""schemas"" section of the Interface, like this:");
                Console.WriteLine();
                Console.WriteLine(@"  ""schemas"": [");
                Console.WriteLine(@"    {");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"      \"@id\": \"dtmi:foo:bar:sharedSchemas:{exampleName};1\",");
                Console.ResetColor();
                Console.WriteLine(@"      ""@type"": . . .");
                Console.WriteLine(@"    }");
                Console.WriteLine(@"  ]");
                Console.WriteLine();
                Console.WriteLine(@"and then refer to the identifier (instead of an inline definition) from multiple places:");
                Console.WriteLine();
                Console.WriteLine($"    \"name\": \"{preName}\",");
                Console.Write(@"    ""schema"":");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($" \"dtmi:foo:bar:sharedSchemas:{exampleName};1\",");
                Console.ResetColor();

                Console.WriteLine();

                return false;
            }

            return true;
        }
    }
}
