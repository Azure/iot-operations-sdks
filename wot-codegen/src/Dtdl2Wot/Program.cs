// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

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
                Console.WriteLine("  [resolverFilePath]    Optional path to a JSON file that defines DTMI resolution rules.");
                Console.WriteLine("  [schemaNamesFilePath] Optional path to a JSON file that defines schema naming rules.");
                Console.WriteLine("                        If not specified, default path is 'SchemaNames.json' in the output folder.");
                Console.WriteLine("                        If file does not exist, one will be created (using specified or default path).");
                return 1;
            }

            FileInfo inputFile = new FileInfo(args[0]);
            DirectoryInfo outputDirectory = new DirectoryInfo(args[1]);
            string resolverFilePath = args.Length > 2 ? args[2] : string.Empty;
            FileInfo schemaNamesFile = new FileInfo(args.Length > 3 ? args[3] : Path.Combine(outputDirectory.FullName, "SchemaNames.json"));

            string modelText = inputFile.OpenText().ReadToEnd();

            DtdlParseLocator parseLocator = (int parseIndex, int parseLine, out string sourceName, out int sourceLine) =>
            {
                sourceName = inputFile.Name;
                sourceLine = parseLine;
                return true;
            };

            ParsingOptions parsingOptions = new();
            if (!string.IsNullOrEmpty(resolverFilePath) && File.Exists(resolverFilePath))
            {
                parsingOptions.DtmiResolver = new Resolver(resolverFilePath).Resolve;
            }
            parsingOptions.ExtensionLimitContexts = new List<Dtmi> { new Dtmi("dtmi:dtdl:limits:onvif"), new Dtmi("dtmi:dtdl:limits:aio") };

            var modelParser = new ModelParser(parsingOptions);

            IReadOnlyDictionary<Dtmi, DTEntityInfo> model = modelParser.Parse(modelText, parseLocator);
            string schemaNamesPath = Path.GetRelativePath(outputDirectory.FullName, schemaNamesFile.FullName).Replace('\\', '/');

            List<DTInterfaceInfo> thingInterfaces = model.Values.Where(dt => dt.EntityKind == DTEntityKind.Interface && dt.SupplementalTypes.Any(t => DtdlMqttExtensionValues.MqttAdjunctTypeRegex.IsMatch(t.AbsoluteUri))).Cast<DTInterfaceInfo>().ToList();

            ITemplateTransform transform = thingInterfaces.Count == 1 ?
                new InterfaceThing(model, thingInterfaces[0].Id, GetMqttVersion(thingInterfaces[0]), schemaNamesPath) :
                new ModelSet(inputFile.Name, thingInterfaces.Select(i => new InterfaceThing(model, i.Id, GetMqttVersion(i), schemaNamesPath)).Cast<ITemplateTransform>().ToList());

            ThingGenerator thingGenerator = new ThingGenerator(transform, outputDirectory);
            thingGenerator.GenerateThing();

            if (!schemaNamesFile.Exists)
            {
                Stream schemaNamesStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dtdl2Wot.Resources.conversion.SchemaNames.json")!;
                string schemaNamesText = new StreamReader(schemaNamesStream).ReadToEnd();
                File.WriteAllText(schemaNamesFile.FullName, schemaNamesText);

                Console.WriteLine($"  generated {schemaNamesFile.FullName}");
            }

            return 0;
        }

        private static int GetMqttVersion(DTInterfaceInfo dtInterface)
        {
            Dtmi mqttTypeId = dtInterface.SupplementalTypes.First(t => DtdlMqttExtensionValues.MqttAdjunctTypeRegex.IsMatch(t.AbsoluteUri));
            return int.Parse(DtdlMqttExtensionValues.MqttAdjunctTypeRegex.Match(mqttTypeId.AbsoluteUri).Groups[1].Captures[0].Value);
        }
    }
}
