// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using VDS.RDF;
    using VDS.RDF.Parsing;

    public static class UnitMapper
    {
        private static readonly Dictionary<string, Uri> uneceCodeToUnit;
        private static readonly Dictionary<Uri, List<Uri>> unitToQuantityKinds;

        static UnitMapper()
        {
            uneceCodeToUnit = new Dictionary<string, Uri>();
            unitToQuantityKinds = new Dictionary<Uri, List<Uri>>();

            IGraph graph = new Graph();

            TurtleParser parser = new TurtleParser();
            string unitResourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.Resources.qudt.unit.ttl";
            Stream unitStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(unitResourceName)!;
            parser.Load(graph, new StreamReader(unitStream));

            IUriNode uneceCommonCodePred = graph.CreateUriNode("qudt:uneceCommonCode");
            IUriNode hasQuantityKindPred = graph.CreateUriNode("qudt:hasQuantityKind");

            Dictionary<Uri, int> quantityKindToUnitCount = new Dictionary<Uri, int>();

            foreach (Triple triple in graph.Triples)
            {
                if (triple.Subject is IUriNode unitNode)
                {
                    if (triple.Predicate.Equals(uneceCommonCodePred) && triple.Object is ILiteralNode uneceCommonCode)
                    {
                        uneceCodeToUnit[uneceCommonCode.Value] = unitNode.Uri;
                    }

                    if (triple.Predicate.Equals(hasQuantityKindPred) && triple.Object is IUriNode quantityKindNode)
                    {
                        if (!unitToQuantityKinds.TryGetValue(unitNode.Uri, out List<Uri>? quantityKinds))
                        {
                            quantityKinds = new List<Uri>();
                            unitToQuantityKinds[unitNode.Uri] = quantityKinds;
                        }
                        quantityKinds.Add(quantityKindNode.Uri);

                        if (!quantityKindToUnitCount.ContainsKey(quantityKindNode.Uri))
                        {
                            quantityKindToUnitCount[quantityKindNode.Uri] = 0;
                        }
                        quantityKindToUnitCount[quantityKindNode.Uri] += 1;
                    }
                }
            }

            foreach (KeyValuePair<Uri, List<Uri>> unitAndQuantityKinds in unitToQuantityKinds)
            {
                unitAndQuantityKinds.Value.Sort((x, y) => quantityKindToUnitCount[y].CompareTo(quantityKindToUnitCount[x]));
            }
        }

        public static string? GetQuantityKindFromUnitId(int unitId)
        {
            string uneceCode = GetUneceCodeFromUnitId(unitId);

            if (uneceCodeToUnit.TryGetValue(uneceCode, out Uri? unit))
            {
                if (unitToQuantityKinds.TryGetValue(unit, out List<Uri>? quantityKinds) && quantityKinds.Count > 0)
                {
                    if (quantityKinds.Count == 1)
                    {
                        return quantityKinds[0].ToString();
                    }

                    return quantityKinds[0].ToString();
                }
            }

            return null;
        }

        private static int GetUnitIdFromUneceCode(string uneceCode)
        {
            int unitId = 0;
            for (int i = 0; i < uneceCode.Length; i++)
            {
                unitId = (unitId << 8) | uneceCode[i];
            }

            return unitId;
        }

        private static string GetUneceCodeFromUnitId(int unitId)
        {
            string uneceCode = string.Empty;
            for (int i = 2; i >= 0; i--)
            {
                char c = (char)((unitId >> (i * 8)) & 0xFF);
                if (c != '\0')
                {
                    uneceCode += c;
                }
            }

            return uneceCode;
        }
    }
}
