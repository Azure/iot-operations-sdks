namespace UnitMatcher
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using CsvHelper;

    public class EnumValueInfo
    {
        [JsonPropertyName("@id")]
        public string? Id { get; set; }

        [JsonPropertyName("@type")]
        public IList<string>? Type { get; set; }

        public string? Name { get; set; }

        public string? EnumValue { get; set; }

        public string? DisplayName { get; set; }

        public string? Symbol { get; set; }

        public string? BaseUnit { get; set; }

        public string? Prefix { get; set; }

        public string? TopUnit { get; set; }

        public string? BottomUnit { get; set; }

        public int? Exponent { get; set; }
    }

    public class EnumInfo
    {
        [JsonPropertyName("@context")]
        public IList<string>? Context { get; set; }

        [JsonPropertyName("@id")]
        public string? Id { get; set; }

        [JsonPropertyName("@type")]

        public string? Type { get; set; }

        public string? ValueSchema { get; set; }

        public IList<EnumValueInfo>? EnumValues { get; set; }

        public string? DisplayName { get; set; }
    }

    public class UneceUnit
    {
        public string? UNECECode { get; set; }

        public int UnitId { get; set; }

        public string? DisplayName { get; set; }

        public string? Description { get; set; }
    }

    public record DtdlUnit(string Type, string Name, string DisplayName, string Symbol);

    internal class Program
    {
        private const string dtdlUnitsFileName = "DTDL.FeatureExtension.quantitativeTypes.v2.Elements.json";
        private const string uaUnitsRelPath = "Schema/UNECE_to_OPCUA.csv";

        private static Dictionary<string, int> decimalPrefixes = new ();
        private static Dictionary<string, int> binaryPrefixes = new ();
        private static Dictionary<string, string> unitPrefixMap = new ();

        private static string Decamel(string s) => Regex.Replace(s, @"([a-z])([A-Z])", (m) => $"{m.Groups[1].Captures[0].Value} {m.Groups[2].Captures[0].Value.ToLower()}");

        private static string Debracket(string s) => s.IndexOf(" [") > 0 ? s.Substring(0, s.IndexOf(" [")) : s;

        private static string RegularizeUaUnitDesc(string s) => Debracket(MapPrefixes(s)
            .Replace(" (US)", "")
            .Replace(" (UK)", "")
            .Replace(" (statute mile)", "")
            .Replace("[unit of angle]", "of arc")
            .Replace("reciprocal second", "hertz")
            .Replace("revolutions", "revolution"));

        private static string RegularizeDtdlUnitDispName(string s) => Decamel(MapPrefixes(s)
            .Replace("inches", "inch")
            .Replace("parts", "part"));

        private static double MatchQuality(DtdlUnit dtdlUnit, UneceUnit uneceUnit)
        {
            return 1.0 - ScaledLevenshteinDistance(RegularizeDtdlUnitDispName(dtdlUnit.DisplayName!), RegularizeUaUnitDesc(uneceUnit.Description!));
        }

        private static double SecondaryMatchQuality(DtdlUnit dtdlUnit, UneceUnit uneceUnit)
        {
            double noSepDist = ScaledLevenshteinDistance(dtdlUnit.Symbol, Debracket(uneceUnit.DisplayName!.Replace(" (US)", "").Replace(" (UK)", "").Replace(" ", "").Replace("·", "").Trim()));
            double lowerDist = ScaledLevenshteinDistance(dtdlUnit.Symbol.ToLower(), Debracket(uneceUnit.DisplayName!.Replace(" (US)", "").Replace(" (UK)", "").Replace(" ", "").ToLower().Trim()));
            return 1.0 - Math.Min(noSepDist, lowerDist);
        }

        static string MapPrefixes(string inDesc)
        {
            string outDesc = inDesc;
            foreach (KeyValuePair<string, string> kvp in unitPrefixMap)
            {
                outDesc = outDesc.Replace(kvp.Key, kvp.Value);
            }
            return outDesc;
        }

        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("usage: UnitMatcher <UA_ROOT> <DTDL_QUANT_ROOT> <OUT_FILE>");
                return;
            }

            string uaRoot = args[0];
            string dtdlQuantRoot = args[1];
            string outFilePath = args[2];

            List<EnumInfo>? enumInfos;
            List<UneceUnit>? uaUnits;

            using (StreamReader dtdlUnitsReader = File.OpenText(Path.Combine(dtdlQuantRoot, dtdlUnitsFileName)))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                enumInfos = JsonSerializer.Deserialize<List<EnumInfo>>(dtdlUnitsReader.ReadToEnd(), options);
            }

            using (StreamReader uaUnitsReader = File.OpenText(Path.Combine(uaRoot, uaUnitsRelPath)))
            {
                using (var csvReader = new CsvReader(uaUnitsReader, CultureInfo.InvariantCulture))
                {
                    uaUnits = csvReader.GetRecords<UneceUnit>().ToList();
                }
            }

            List<DtdlUnit> dtdlUnits = GetDtdlUnits(enumInfos!);
            decimalPrefixes = GetUnitPrefixes(enumInfos!, "DecimalPrefix");
            binaryPrefixes = GetUnitPrefixes(enumInfos!, "BinaryPrefix");
            unitPrefixMap = binaryPrefixes.ToDictionary(b => b.Key, b => decimalPrefixes.First(d => d.Value * 10 == b.Value * 3).Key);

            List<(UneceUnit, DtdlUnit, double, double)> matches = new();
            HashSet<string> matchedDtdlUnits = new ();

            foreach (UneceUnit uneceUnit in uaUnits)
            {
                DtdlUnit bestMatchingDtdlUnit = dtdlUnits.Aggregate((max, cur) => MatchQuality(cur, uneceUnit) > MatchQuality(max, uneceUnit) ? cur : max);

                // Prefx mismatch implies units do not match
                if (TryGetPrefix(decimalPrefixes, uneceUnit.Description!, out int uneceExponent) && (TryGetPrefix(decimalPrefixes, bestMatchingDtdlUnit.DisplayName, out int dtdlExponent) && uneceExponent != dtdlExponent))
                {
                    continue;
                }

                // Known unit mismatch implies units do not match
                int uneceKnownIndex = GetKnownUnit(uneceUnit.Description!);
                int dtdlKnownIndex = GetKnownUnit(bestMatchingDtdlUnit.DisplayName);
                if (uneceKnownIndex > 0 && dtdlKnownIndex > 0 && uneceKnownIndex != dtdlKnownIndex)
                {
                    continue;
                }

                double matchQual = MatchQuality(bestMatchingDtdlUnit, uneceUnit);
                double secMatchQual = SecondaryMatchQuality(bestMatchingDtdlUnit, uneceUnit);

                if (matchQual == 1.0 ||
                    secMatchQual == 1.0 ||
                    matchQual > 0.9 && secMatchQual > 0.6 ||
                    matchQual > 0.8 && secMatchQual > 0.8 ||
                    matchQual > 0.6 && secMatchQual > 0.9)
                {
                    matches.Add((uneceUnit, bestMatchingDtdlUnit, matchQual, secMatchQual));
                    matchedDtdlUnits.Add(bestMatchingDtdlUnit.Name);
                }
            }

            using (StreamWriter outputFile = new StreamWriter(outFilePath))
            {
                matches.Sort((x, y) => x.Item1.UnitId == y.Item1.UnitId ? 0 : x.Item1.UnitId < y.Item1.UnitId ? -1 : 1);

                foreach ((UneceUnit, DtdlUnit, double, double) match in matches)
                {
                    outputFile.WriteLine($"{match.Item1.UnitId},{UnitTypeToSemanticType(match.Item2.Type)},{match.Item2.Name}");
                }
            }

            Console.WriteLine("Unmatched DTDL units:");
            foreach (DtdlUnit dtdlUnit in dtdlUnits)
            {
                if (!matchedDtdlUnits.Contains(dtdlUnit.Name))
                {
                    Console.WriteLine($"{dtdlUnit.Type}, {dtdlUnit.Name}");
                }
            }
        }

        private static List<DtdlUnit> GetDtdlUnits(List<EnumInfo> enumInfos)
        {
            List<DtdlUnit> dtdlUnits = new();

            foreach (EnumInfo enumInfo in enumInfos!)
            {
                if (!enumInfo.Id!.EndsWith("Unit") && !enumInfo.Id!.EndsWith("Unitless"))
                {
                    continue;
                }

                foreach (EnumValueInfo enumValueInfo in enumInfo.EnumValues!)
                {
                    dtdlUnits.Add(new DtdlUnit(enumInfo.DisplayName!, enumValueInfo.Name!, enumValueInfo.DisplayName!, enumValueInfo.Symbol!));
                }
            }

            return dtdlUnits;
        }

        private static Dictionary<string, int> GetUnitPrefixes(List<EnumInfo> enumInfos, string unitBase)
        {
            Dictionary<string, int> decimalPrefixes = new();

            EnumInfo enumInfo = enumInfos!.First(e => e.Id!.EndsWith(unitBase));
            foreach (EnumValueInfo enumValueInfo in enumInfo.EnumValues!)
            {
                decimalPrefixes[enumValueInfo.Name!] = (int)enumValueInfo.Exponent!;
            }

            return decimalPrefixes;
        }

        private static string UnitTypeToSemanticType(string unitType)
        {
            return unitType switch
            {
                "Unitless" => "Concentration",
                "TimeUnit" => "TimeSpan",
                _ => unitType.Substring(0, unitType.Length - "Unit".Length)
            };
        }

        private static bool TryGetPrefix(Dictionary<string, int> decimalPrefixes, string unitName, out int exponent)
        {
            foreach (KeyValuePair<string, int> prefixPair in decimalPrefixes)
            {
                if (unitName.StartsWith(prefixPair.Key) || unitName.Contains($" {prefixPair.Key}"))
                {
                    exponent = prefixPair.Value;
                    return true;
                }
            }

            exponent = 0;
            return false;
        }

        private static int GetKnownUnit(string unitName)
        {
            return unitName switch
            {
                string u when u.Contains("mole") => 1,
                string u when u.Contains("mile") => 2,
                string u when u.Contains("metre") => 3,
                string u when u.Contains("litre") => 4,
                string u when u.Contains("gray") => 5,
                string u when u.Contains("gram") => 6,
                string u when u.Contains("foot") => 7,
                _ => 0,
            };
        }

        private static double ScaledLevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
            {
                return 0;
            }

            if (string.IsNullOrEmpty(a))
            {
                return b.Length;
            }

            if (string.IsNullOrEmpty(b))
            {
                return a.Length;
            }

            int lengthA = a.Length;
            int lengthB = b.Length;
            var distances = new int[lengthA + 1, lengthB + 1];

            for (int i = 0; i <= lengthA; distances[i, 0] = i++) ;
            for (int j = 0; j <= lengthB; distances[0, j] = j++) ;

            for (int i = 1; i <= lengthA; i++)
            {
                for (int j = 1; j <= lengthB; j++)
                {
                    int cost = b[j - 1] == a[i - 1] ? 0 : 1;

                    distances[i, j] = Math.Min(
                        Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                        distances[i - 1, j - 1] + cost
                    );
                }
            }

            int levenshteinDistance = distances[lengthA, lengthB];

            return (double)levenshteinDistance / (double)Math.Max(lengthA, lengthB);
        }
    }
}
