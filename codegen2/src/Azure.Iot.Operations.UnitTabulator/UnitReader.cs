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

            IGraph graph = new Graph();

            TurtleParser parser = new TurtleParser();
            string unitResourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.Resources.qudt.unit.ttl";
            Stream unitStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(unitResourceName)!;
            parser.Load(graph, new StreamReader(unitStream));

            IUriNode uneceCommonCodePred = graph.CreateUriNode("qudt:uneceCommonCode");
            IUriNode hasQuantityKindPred = graph.CreateUriNode("qudt:hasQuantityKind");
            IUriNode conversionMultiplierPred = graph.CreateUriNode("qudt:conversionMultiplierSN");
            IUriNode conversionOffsetPred = graph.CreateUriNode("qudt:conversionOffsetSN");
            IUriNode typePred = graph.CreateUriNode("rdf:type");
            IUriNode deprecatedPred = graph.CreateUriNode("qudt:deprecated");
            IUriNode labelPred = graph.CreateUriNode("rdfs:label");

            IUriNode unitObject = graph.GetUriNode("qudt:Unit");

            foreach (Triple unitTriple in graph.GetTriplesWithPredicateObject(typePred, unitObject))
            {
                IUriNode unitSubject = (IUriNode)unitTriple.Subject;
                if (graph.GetTriplesWithSubjectPredicate(unitSubject, deprecatedPred).Any())
                {
                    continue;
                }

                string unitName = UriTail(unitSubject.Uri);

                foreach (Triple eceCodeTriple in graph.GetTriplesWithSubjectPredicate(unitSubject, uneceCommonCodePred))
                {
                    if (eceCodeTriple.Object is ILiteralNode uneceCommonCode)
                    {
                        EceCodesMap[uneceCommonCode.Value] = unitName;
                    }
                }

                IEnumerable<Triple> unitHasQuantityKindTriple = graph.GetTriplesWithSubjectPredicate(unitSubject, hasQuantityKindPred);
                int maxUnitCount = unitHasQuantityKindTriple.Max(t => quantityKindToUnitCount[((IUriNode)t.Object).Uri]);
                Uri quantityKindUri = ((IUriNode)unitHasQuantityKindTriple.First(t => quantityKindToUnitCount[((IUriNode)t.Object).Uri] == maxUnitCount).Object).Uri;

                double multiplier = 1.0;
                if (graph.GetTriplesWithSubjectPredicate(unitSubject, conversionMultiplierPred).FirstOrDefault()?.Object is ILiteralNode multiplierNode)
                {
                    multiplier = double.Parse(multiplierNode.Value, System.Globalization.CultureInfo.InvariantCulture);
                }

                double offset = 0.0;
                if (graph.GetTriplesWithSubjectPredicate(unitSubject, conversionOffsetPred).FirstOrDefault()?.Object is ILiteralNode offsetNode)
                {
                    offset = double.Parse(offsetNode.Value, System.Globalization.CultureInfo.InvariantCulture);
                }

                IEnumerable<Triple> unitLabelTriples = graph.GetTriplesWithSubjectPredicate(unitSubject, labelPred);
                Triple? labelTriple =
                    unitLabelTriples.FirstOrDefault(t => t.Object is ILiteralNode l && l.Language == "en-US") ??
                    unitLabelTriples.FirstOrDefault(t => t.Object is ILiteralNode l && l.Language == "en") ??
                    unitLabelTriples.FirstOrDefault(t => t.Object is ILiteralNode l && l.Language == string.Empty);
                string? label = labelTriple != null ? ((ILiteralNode)labelTriple.Object).Value : null;

                UnitInfosMap[unitName] = new UnitInfo(UriTail(quantityKindUri), multiplier, offset, label);
            }
        }

        public Dictionary<string, string> EceCodesMap { get; }

        public Dictionary<string, UnitInfo> UnitInfosMap { get; }

        private string UriTail(Uri uri) => uri.AbsoluteUri.Substring(uri.AbsoluteUri.LastIndexOf('/') + 1);
    }
}
