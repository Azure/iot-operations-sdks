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
                Console.WriteLine("Usage: Dtdl2Wot <inputFilePath> <outputFolderPath>");
                Console.WriteLine("Converts a DTDL model file to a WoT Thing Description.");
                return 1;
            }

            string inputFilePath = args[0];
            string outputFolderPath = args[1];

            string modelText = File.ReadAllText(inputFilePath);

            DtdlParseLocator parseLocator = (int parseIndex, int parseLine, out string sourceName, out int sourceLine) =>
            {
                sourceName = inputFilePath;
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

            DirectoryInfo outDir = new DirectoryInfo(outputFolderPath);

            string schemaNamesText = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Dtdl2Wot.Resources.conversion.SchemaNames.json")!).ReadToEnd();
            File.WriteAllText(Path.Combine(outDir.FullName, "SchemaNames.json"), schemaNamesText);

            return thingGenerator.GenerateThing(outDir) ? 0 : 1;
        }
    }
}
