// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2Wot
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    public class IntegralResolvingThing : IResolvingThing
    {
        private Dictionary<string, TDThing> hrefToThingMap;
        private ErrorReporter errorReporter;

        public IntegralResolvingThing(TDThing thing, ErrorReporter errorReporter, Dictionary<string, TDThing> hrefToThingMap)
        {
            this.hrefToThingMap = hrefToThingMap;
            this.errorReporter = errorReporter;
            ParsedThing = new ParsedThing(thing, string.Empty, string.Empty, new SchemaNamer(null), errorReporter, true, true);
        }

        public ParsedThing ParsedThing { get; }

        public bool TryResolve(string href, [NotNullWhen(true)] out IResolvingThing? resolvingThing)
        {
            if (hrefToThingMap.TryGetValue(href, out TDThing? referencedThing))
            {
                resolvingThing = new IntegralResolvingThing(referencedThing, errorReporter, hrefToThingMap);
                return true;
            }

            resolvingThing = null;
            return false;
        }
    }
}
