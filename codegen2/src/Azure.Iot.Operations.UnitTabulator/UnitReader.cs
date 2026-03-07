// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using VDS.RDF;
    using VDS.RDF.Parsing;

    public class UnitReader
    {
        private readonly Dictionary<Uri, List<Uri>> unitToQuantityKinds;

        public UnitReader()
        {
            QuantityKindReader quantityKindReader = new QuantityKindReader();
            Dictionary<Uri, int> quantityKindToUnitCount = quantityKindReader.QuantityKindToUnitCount;

            unitToQuantityKinds = new Dictionary<Uri, List<Uri>>();

            EceCodesMap = new Dictionary<string, string>();
            UnitInfosMap = new Dictionary<string, UnitInfo>();
            KindLabeledUnitsMap = new Dictionary<string, List<(string, string)>>();
            KindSystemUnitsMap = new Dictionary<(string, string), string>();
            LabeledSystemsOfUnits = new List<(string, string)>();

            Dictionary<(string, string), List<string>> kindSystemApplicableUnits = new Dictionary<(string, string), List<string>>();

            TurtleParser parser = new TurtleParser();

            IGraph unitGraph = new Graph();
            string unitResourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.Resources.qudt.unit.ttl";
            Stream unitStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(unitResourceName)!;
            parser.Load(unitGraph, new StreamReader(unitStream));

            IGraph souGraph = new Graph();
            string souResourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.Resources.qudt.sou.ttl";
            Stream souStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(souResourceName)!;
            parser.Load(souGraph, new StreamReader(souStream));

            IUriNode uneceCommonCodePred = unitGraph.CreateUriNode("qudt:uneceCommonCode");
            IUriNode hasQuantityKindPred = unitGraph.CreateUriNode("qudt:hasQuantityKind");
            IUriNode conversionMultiplierPred = unitGraph.CreateUriNode("qudt:conversionMultiplierSN");
            IUriNode conversionOffsetPred = unitGraph.CreateUriNode("qudt:conversionOffsetSN");
            IUriNode typePred = unitGraph.CreateUriNode("rdf:type");
            IUriNode deprecatedPred = unitGraph.CreateUriNode("qudt:deprecated");
            IUriNode labelPred = unitGraph.CreateUriNode("rdfs:label");
            IUriNode applicableSystemPred = unitGraph.CreateUriNode("qudt:applicableSystem");

            IUriNode unitObject = unitGraph.GetUriNode("qudt:Unit");
            foreach (Triple unitTriple in unitGraph.GetTriplesWithPredicateObject(typePred, unitObject))
            {
                IUriNode unitSubject = (IUriNode)unitTriple.Subject;
                if (unitGraph.GetTriplesWithSubjectPredicate(unitSubject, deprecatedPred).Any())
                {
                    continue;
                }

                string unitName = UriTail(unitSubject.Uri);

                foreach (Triple eceCodeTriple in unitGraph.GetTriplesWithSubjectPredicate(unitSubject, uneceCommonCodePred))
                {
                    if (eceCodeTriple.Object is ILiteralNode uneceCommonCode)
                    {
                        EceCodesMap[uneceCommonCode.Value] = unitName;
                    }
                }

                IEnumerable<Triple> unitHasQuantityKindTriples = unitGraph.GetTriplesWithSubjectPredicate(unitSubject, hasQuantityKindPred).Where(t => t.Object is IUriNode objUriNode && objUriNode.Uri.AbsoluteUri != "http://qudt.org/vocab/quantitykind/Unknown");
                if (!unitHasQuantityKindTriples.Any())
                {
                    continue;
                }

                int maxUnitCount = unitHasQuantityKindTriples.Max(t => quantityKindToUnitCount[((IUriNode)t.Object).Uri]);
                INode quantityKindNode = unitHasQuantityKindTriples.First(t => quantityKindToUnitCount[((IUriNode)t.Object).Uri] == maxUnitCount).Object;
                string quantityKindName = UriTail(((IUriNode)quantityKindNode).Uri);

                double multiplier = 1.0;
                if (unitGraph.GetTriplesWithSubjectPredicate(unitSubject, conversionMultiplierPred).FirstOrDefault()?.Object is ILiteralNode multiplierNode)
                {
                    multiplier = double.Parse(multiplierNode.Value, System.Globalization.CultureInfo.InvariantCulture);
                }

                double offset = 0.0;
                if (unitGraph.GetTriplesWithSubjectPredicate(unitSubject, conversionOffsetPred).FirstOrDefault()?.Object is ILiteralNode offsetNode)
                {
                    offset = double.Parse(offsetNode.Value, System.Globalization.CultureInfo.InvariantCulture);
                }

                IEnumerable<Triple> unitLabelTriples = unitGraph.GetTriplesWithSubjectPredicate(unitSubject, labelPred);
                Triple? labelTriple =
                    unitLabelTriples.FirstOrDefault(t => t.Object is ILiteralNode l && l.Language == "en-US") ??
                    unitLabelTriples.FirstOrDefault(t => t.Object is ILiteralNode l && l.Language == "en") ??
                    unitLabelTriples.FirstOrDefault(t => t.Object is ILiteralNode l && l.Language == string.Empty);
                string? label = labelTriple != null ? ((ILiteralNode)labelTriple.Object).Value : null;

                UnitInfosMap[unitName] = new UnitInfo(quantityKindName, multiplier, offset, label);

                if (!KindLabeledUnitsMap.TryGetValue(quantityKindName, out List<(string, string)>? labeledUnits))
                {
                    labeledUnits = new List<(string, string)>();
                    KindLabeledUnitsMap[quantityKindName] = labeledUnits;
                }
                labeledUnits.Add((unitName, label ?? unitName));

                foreach (var unitSystemTriple in unitGraph.GetTriplesWithSubjectPredicate(unitSubject, applicableSystemPred))
                {
                    if (unitSystemTriple.Object is IUriNode systemNode)
                    {
                        string systemName = UriTail(systemNode.Uri);
                        if (!kindSystemApplicableUnits.TryGetValue((quantityKindName, systemName), out List<string>? applicableUnits))
                        {
                            applicableUnits = new List<string>();
                            kindSystemApplicableUnits[(quantityKindName, systemName)] = applicableUnits;
                        }

                        kindSystemApplicableUnits[(quantityKindName, systemName)].Add(unitName);
                    }
                }
            }

            IUriNode hasBaseUnitPred = souGraph.CreateUriNode("qudt:hasBaseUnit");

            IUriNode souObject = souGraph.GetUriNode("qudt:SystemOfUnits");
            foreach (Triple souTriple in souGraph.GetTriplesWithPredicateObject(typePred, souObject))
            {
                IUriNode souSubject = (IUriNode)souTriple.Subject;
                string souName = UriTail(souSubject.Uri);

                IEnumerable<Triple> souLabelTriples = souGraph.GetTriplesWithSubjectPredicate(souSubject, labelPred);
                Triple labelTriple = souLabelTriples.First(t => t.Object is ILiteralNode l && l.Language == "en");
                string? label = ((ILiteralNode)labelTriple.Object).Value;

                LabeledSystemsOfUnits.Add((souName, label));

                foreach (var systemUnitTriple in souGraph.GetTriplesWithSubjectPredicate(souSubject, hasBaseUnitPred))
                {
                    if (systemUnitTriple.Object is IUriNode unitNode)
                    {
                        string unitName = UriTail(unitNode.Uri);
                        KindSystemUnitsMap[(UnitInfosMap[unitName].Kind, souName)] = unitName;
                    }
                }
            }

            foreach (KeyValuePair<(string, string), List<string>> kindSystemApplicableUnitsEntry in kindSystemApplicableUnits)
            {
                string quantityKindName = kindSystemApplicableUnitsEntry.Key.Item1;
                string systemName = kindSystemApplicableUnitsEntry.Key.Item2;

                if (!KindSystemUnitsMap.ContainsKey((quantityKindName, systemName)))
                {
                    List<string> applicableUnits = kindSystemApplicableUnitsEntry.Value.OrderBy(u => u.Length).ToList();
                    KindSystemUnitsMap[(quantityKindName, systemName)] = applicableUnits[0];
                }
            }
        }

        public Dictionary<string, string> EceCodesMap { get; }

        public Dictionary<string, UnitInfo> UnitInfosMap { get; }

        public Dictionary<string, List<(string, string)>> KindLabeledUnitsMap { get; }

        public Dictionary<(string, string), string> KindSystemUnitsMap { get; }

        public List<(string, string)> LabeledSystemsOfUnits {  get; }

        private string UriTail(Uri uri) => uri.AbsoluteUri.Substring(uri.AbsoluteUri.LastIndexOf('/') + 1);
    }
}
