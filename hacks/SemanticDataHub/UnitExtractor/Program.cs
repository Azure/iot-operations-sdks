namespace UnitExtractor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Text.RegularExpressions;
    using DTDLParser;
    using DTDLParser.Models;

    internal class Program
    {
        private const string mqttAdjunctTypePattern = @"dtmi:dtdl:extension:mqtt:v\d+:Mqtt";
        private const string quantitativeTypePattern = @"dtmi:dtdl:extension:quantitativeTypes:v(\d+):class:(\w+)";
        private const string quantitativeUnitPropertyFormat = @"dtmi:dtdl:extension:quantitativeTypes:v{0}:property:unit";

        private static Regex mqttAdjunctTypeRegex = new Regex(mqttAdjunctTypePattern, RegexOptions.Compiled);
        private static Regex quantitativeTypeRegex = new Regex(quantitativeTypePattern, RegexOptions.Compiled);

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("UnitExtractor <DTDL_FILENAME> <UNIT_FILENAME>");
                return;
            }

            string inPath = args[0];
            string outPath = args[1];

            ModelParser modelParser = new ModelParser();

            string fileText = File.ReadAllText(inPath);

            DtdlParseLocator parseLocator = (int parseIndex, int parseLine, out string sourceName, out int sourceLine) =>
            {
                sourceName = inPath;
                sourceLine = parseLine;
                return true;
            };

            DTInterfaceInfo dtInterface;
            try
            {
                IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = modelParser.Parse(fileText, parseLocator);

                IEnumerable<DTInterfaceInfo> mqttInterfaces = modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Interface && e.SupplementalTypes.Any(t => mqttAdjunctTypeRegex.IsMatch(t.AbsoluteUri))).Select(e => (DTInterfaceInfo)e);
                switch (mqttInterfaces.Count())
                {
                    case 0:
                        Console.Write($"No Interface in model has a co-type of 'Mqtt'");
                        return;
                    case 1:
                        dtInterface = mqttInterfaces.First();
                        break;
                    default:
                        Console.Write($"More than one Interface has a co-type of 'Mqtt'");
                        return;
                }
            }
            catch (ParsingException ex)
            {
                foreach (ParsingError err in ex.Errors)
                {
                    Console.WriteLine(err.Message);
                }

                return;
            }

            Dictionary<string, string> pathUnitMap = new ();
            foreach (KeyValuePair<string, DTTelemetryInfo> telem in dtInterface.Telemetries)
            {
                RecordQuantitativeTypeAndUnit(telem.Value.Name, telem.Value, pathUnitMap);
            }

            using (StreamWriter streamWriter = new StreamWriter(outPath))
            {
                streamWriter.WriteLine("{");

                int ix = 1;
                foreach (KeyValuePair<string, string> pathUnit in pathUnitMap)
                {
                    streamWriter.WriteLine($"  \"{pathUnit.Key}\": \"{pathUnit.Value}\"{(ix < pathUnitMap.Count ? "," : "")}");
                    ++ix;
                }

                streamWriter.WriteLine("}");
            }

            Console.WriteLine($"  extracted quantitative types and units from model into {outPath}");
        }

        private static void RecordQuantitativeTypeAndUnit(string propertyPath, DTTelemetryInfo dtTelemetry, Dictionary<string, string> pathUnitMap)
        {
            Dtmi? quantitativeTypeId = dtTelemetry.SupplementalTypes.FirstOrDefault(t => quantitativeTypeRegex.IsMatch(t.AbsoluteUri));
            if (quantitativeTypeId != null)
            {
                pathUnitMap[propertyPath] = GetTypeAndUnitSpecifier(quantitativeTypeId, dtTelemetry.SupplementalProperties);
                return;
            }

            RecordQuantitativeTypeAndUnit(propertyPath, dtTelemetry.Schema, pathUnitMap);
        }

        private static void RecordQuantitativeTypeAndUnit(string propertyPath, DTSchemaFieldInfo dtSchemaField, Dictionary<string, string> pathUnitMap)
        {
            Dtmi? quantitativeTypeId = dtSchemaField.SupplementalTypes.FirstOrDefault(t => quantitativeTypeRegex.IsMatch(t.AbsoluteUri));
            if (quantitativeTypeId != null)
            {
                pathUnitMap[propertyPath] = GetTypeAndUnitSpecifier(quantitativeTypeId, dtSchemaField.SupplementalProperties);
                return;
            }

            RecordQuantitativeTypeAndUnit(propertyPath, dtSchemaField.Schema, pathUnitMap);
        }

        private static void RecordQuantitativeTypeAndUnit(string propertyPath, DTSchemaInfo dtSchema, Dictionary<string, string> pathUnitMap)
        {
            switch (dtSchema)
            {
                case DTObjectInfo dtObject:
                    foreach (DTFieldInfo dtField in dtObject.Fields)
                    {
                        RecordQuantitativeTypeAndUnit($"{propertyPath}.{dtField.Name}", dtField, pathUnitMap);
                    }
                    break;
                case DTMapInfo dtMap:
                    RecordQuantitativeTypeAndUnit(propertyPath, dtMap.MapValue, pathUnitMap);
                    break;
            }
        }

        private static string GetTypeAndUnitSpecifier(Dtmi quantitativeTypeId, IDictionary<string, object> supplementalProperties)
        {
            Match quantMatch = quantitativeTypeRegex.Match(quantitativeTypeId.AbsoluteUri);
            string quantType = quantMatch.Groups[2].Captures[0].Value;
            int quantExtVersion = int.Parse(quantMatch.Groups[1].Captures[0].Value);
            string unitPropertyId = string.Format(quantitativeUnitPropertyFormat, quantExtVersion);
            DTEnumValueInfo unit = (DTEnumValueInfo)supplementalProperties[unitPropertyId];
            return $"{quantType}({ModelParser.GetTermOrUri(unit.Id)})";
        }
    }
}
