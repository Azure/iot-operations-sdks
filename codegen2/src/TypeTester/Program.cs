namespace TypeTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TypeGenerator;

    internal class Program
    {
        static Dictionary<SerializationFormat, string> FormatFilters = new()
        {
            { SerializationFormat.Json, "*.json" },
        };

        static void Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: TypeTester <schema folder> <output folder> <project name> C#|Rust");
                return;
            }

            DirectoryInfo schemaFolder = new DirectoryInfo(args[0]);
            if (!schemaFolder.Exists)
            {
                Console.WriteLine($"Folder not found: {schemaFolder.FullName}");
                return;
            }

            DirectoryInfo outputFolder = new DirectoryInfo(args[1]);

            string projectName = args[2];

            var (targetLanguage, srcSubdir) = args[3].ToLower() switch
            {
                "c#" => (TargetLanguage.CSharp, ""),
                "rust" => (TargetLanguage.Rust, "src"),
                _ => throw new NotSupportedException($"Target language {args[3]} is not supported."),
            };

            TypeNamer typeNamer = new TypeNamer(null);

            foreach (KeyValuePair<SerializationFormat, string> formatFilter in FormatFilters)
            {
                TypeGenerator typeGenerator = new TypeGenerator(formatFilter.Key, targetLanguage, typeNamer);

                Dictionary<string, string> schemaTextsByName = schemaFolder.GetFiles(formatFilter.Value).ToDictionary(f => f.FullName.Replace('\\', '/'), f => File.ReadAllText(f.FullName));

                foreach (GeneratedItem genType in typeGenerator.GenerateTypes(schemaTextsByName, new CodeName("Namespace"), projectName, srcSubdir))
                {
                    DirectoryInfo folderPath = new DirectoryInfo(Path.Combine(outputFolder.FullName, genType.FolderPath));
                    if (!folderPath.Exists)
                    {
                        folderPath.Create();
                    }

                    string filePath = Path.Combine(folderPath.FullName, genType.FileName);
                    File.WriteAllText(filePath, genType.Content);
                    Console.WriteLine($"Generated {filePath}");
                }
            }
        }
    }
}
