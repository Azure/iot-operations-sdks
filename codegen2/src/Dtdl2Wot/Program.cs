namespace Dtdl2Wot
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.IO;
    using DTDLParser;
    using DTDLParser.Models;

    internal class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Dtdl2Wot <inputFilePath> <outputFolderPath> [schemaNamesFilePath]");
                Console.WriteLine("Converts a DTDL model file to a WoT Thing Description.");
                Console.WriteLine("  <inputFilePath>       Path to the input DTDL model file.");
                Console.WriteLine("  <outputFolderPath>    Path to the output folder for the generated Thing Description.");
                Console.WriteLine("  [schemaNamesFilePath] Optional path to a JSON file that defines schema naming rules.");
                Console.WriteLine("                        If not specified, default path is 'SchemaNames.json' in the output folder.");
                Console.WriteLine("                        If file does not exist, one will be created (using specified or default path).");
                return 1;
            }

            FileInfo inputFile = new FileInfo(args[0]);
            DirectoryInfo outputDirectory = new DirectoryInfo(args[1]);
            FileInfo schemaNamesFile = new FileInfo(args.Length > 2 ? args[2] : Path.Combine(outputDirectory.FullName, "SchemaNames.json"));

            string modelText = inputFile.OpenText().ReadToEnd();

            DtdlParseLocator parseLocator = (int parseIndex, int parseLine, out string sourceName, out int sourceLine) =>
            {
                sourceName = inputFile.Name;
                sourceLine = parseLine;
                return true;
            };

            ParsingOptions parsingOptions = new();
            parsingOptions.ExtensionLimitContexts = new List<Dtmi> { new Dtmi("dtmi:dtdl:limits:onvif"), new Dtmi("dtmi:dtdl:limits:aio") };

            var modelParser = new ModelParser(parsingOptions);

            IReadOnlyDictionary<Dtmi, DTEntityInfo> model = modelParser.Parse(modelText, parseLocator);

            DTInterfaceInfo dtInterface = (DTInterfaceInfo)model.Values.FirstOrDefault(e => e.EntityKind == DTEntityKind.Interface && e.SupplementalTypes.Any(t => DtdlMqttExtensionValues.MqttAdjunctTypeRegex.IsMatch(t.AbsoluteUri)))!;
            Dtmi mqttTypeId = dtInterface.SupplementalTypes.First(t => DtdlMqttExtensionValues.MqttAdjunctTypeRegex.IsMatch(t.AbsoluteUri));
            int mqttVersion = int.Parse(DtdlMqttExtensionValues.MqttAdjunctTypeRegex.Match(mqttTypeId.AbsoluteUri).Groups[1].Captures[0].Value);

            ThingGenerator thingGenerator = new ThingGenerator(model, dtInterface.Id, mqttVersion);

            if (!schemaNamesFile.Exists)
            {
                Stream schemaNamesStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dtdl2Wot.Resources.conversion.SchemaNames.json")!;
                string schemaNamesText = new StreamReader(schemaNamesStream).ReadToEnd();
                File.WriteAllText(schemaNamesFile.FullName, schemaNamesText);

                Console.WriteLine($"  generated {schemaNamesFile.FullName}");
            }

            return thingGenerator.GenerateThing(outputDirectory, schemaNamesFile) ? 0 : 1;
        }
    }
}
