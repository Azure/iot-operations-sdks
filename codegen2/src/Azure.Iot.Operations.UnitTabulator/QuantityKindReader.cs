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

    public class QuantityKindReader
    {
        public QuantityKindReader()
        {
            QuantityKindToUnitCount = new Dictionary<Uri, int>();

            IGraph graph = new Graph();

            TurtleParser parser = new TurtleParser();
            string quantityKindResourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.Resources.qudt.quantityKind.ttl";
            Stream quantityKindStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(quantityKindResourceName)!;
            parser.Load(graph, new StreamReader(quantityKindStream));

            IUriNode typePred = graph.CreateUriNode("rdf:type");
            IUriNode applicableUnitPred = graph.CreateUriNode("qudt:applicableUnit");
            IUriNode deprecatedPred = graph.CreateUriNode("qudt:deprecated");

            IUriNode quantityKindObject = graph.GetUriNode("qudt:QuantityKind");

            foreach (Triple quantityKindTriple in graph.GetTriplesWithPredicateObject(typePred, quantityKindObject))
            {
                IUriNode quantityKindSubject = (IUriNode)quantityKindTriple.Subject;
                if (graph.GetTriplesWithSubjectPredicate(quantityKindSubject, deprecatedPred).Any())
                {
                    continue;
                }

                string quantityKindName = quantityKindSubject.Uri.AbsoluteUri.Substring(quantityKindSubject.Uri.AbsoluteUri.LastIndexOf('/') + 1);

                QuantityKindToUnitCount[quantityKindSubject.Uri] = graph.GetTriplesWithSubjectPredicate(quantityKindSubject, applicableUnitPred).Count();
            }
        }

        public Dictionary<Uri, int> QuantityKindToUnitCount { get; }
    }
}
