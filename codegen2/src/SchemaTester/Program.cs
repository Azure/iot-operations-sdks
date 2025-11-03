namespace SchemaTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;
    using Azure.Iot.Operations.SchemaGenerator;

    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: SchemaTester <model folder> <output folder> <project name>");
                return;
            }

            DirectoryInfo modelFolder = new DirectoryInfo(args[0]);
            if (!modelFolder.Exists)
            {
                Console.WriteLine($"Folder not found: {modelFolder.FullName}");
                return;
            }

            DirectoryInfo outputFolder = new DirectoryInfo(args[1]);

            string projectName = args[2];

            Dictionary<string, string> modelTextsByName = modelFolder.GetFiles("*.TD.json").ToDictionary(f => f.Name, f => File.ReadAllText(f.FullName));

            foreach (KeyValuePair<string, string> modelNameAndText in modelTextsByName)
            {
                TDThing? thing = TDParser.Parse(modelNameAndText.Value);
                if (thing == null)
                {
                    Console.WriteLine($"Failed to parse model: {modelNameAndText.Key}");
                    continue;
                }

                Console.WriteLine($"Processing model: {modelNameAndText.Key}");

                string? schemaNamesFilename = thing.Links?.FirstOrDefault(l => l.Relation == TDValues.RelationSchemaNaming)?.Href;
                string? schemaNameInfoText = schemaNamesFilename != null ? File.ReadAllText(Path.Combine(modelFolder.FullName, schemaNamesFilename)) : null;
                SchemaNamer schemaNamer = new SchemaNamer(schemaNameInfoText);

                foreach (KeyValuePair<SerializationFormat, List<GeneratedItem>> schemaSet in SchemaGenerator.GenerateSchemas(new List<ParsedThing> { new ParsedThing(thing, modelFolder.FullName, schemaNamer) }, projectName, outputFolder))
                {
                    foreach (GeneratedItem genSchema in schemaSet.Value)
                    {
                        DirectoryInfo folderPath = new DirectoryInfo(Path.Combine(outputFolder.FullName, genSchema.FolderPath));
                        if (!folderPath.Exists)
                        {
                            folderPath.Create();
                        }

                        string filePath = Path.Combine(folderPath.FullName, genSchema.FileName);
                        File.WriteAllText(filePath, genSchema.Content);
                        Console.WriteLine($"Generated {filePath}");
                    }
                }
            }
        }
    }
}
