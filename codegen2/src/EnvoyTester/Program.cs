namespace EnvoyTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;
    using Azure.Iot.Operations.EnvoyGenerator;

    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("Usage: EnvoyTester <model folder> <output folder> <project name> C#|Rust <sdk path>");
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

            var (targetLanguage, extension, srcSubdir) = args[3].ToLower() switch
            {
                "c#" => (TargetLanguage.CSharp, "cs", ""),
                "rust" => (TargetLanguage.Rust, "rs", "src"),
                _ => throw new NotSupportedException($"Target language {args[3]} is not supported."),
            };

            string sdkPath = Path.GetRelativePath(outputFolder.FullName, args[4]);

            Dictionary<string, string> modelTextsByName = modelFolder.GetFiles("*.TD.json").ToDictionary(f => f.Name, f => File.ReadAllText(f.FullName));

            List<string> typeFileNames = outputFolder.GetFiles($"*.{extension}").Select(p => Path.GetFileNameWithoutExtension(p.Name)).Order().ToList();

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

                List<SerializationFormat> serializationFormats = ThingSupport.GetSerializationFormats(new List<TDThing> { thing });

                foreach (GeneratedItem genEnvoy in EnvoyGenerator.GenerateEnvoys(new List<ParsedThing> { new ParsedThing(thing, schemaNamer) }, serializationFormats, targetLanguage, "Namespace", projectName, sdkPath, typeFileNames, srcSubdir, true, true, true, false))
                {
                    DirectoryInfo folderPath = new DirectoryInfo(Path.Combine(outputFolder.FullName, genEnvoy.FolderPath));
                    if (!folderPath.Exists)
                    {
                        folderPath.Create();
                    }

                    string filePath = Path.Combine(folderPath.FullName, genEnvoy.FileName);
                    File.WriteAllText(filePath, genEnvoy.Content);
                    Console.WriteLine($"Generated {filePath}");
                }
            }
        }
    }
}
