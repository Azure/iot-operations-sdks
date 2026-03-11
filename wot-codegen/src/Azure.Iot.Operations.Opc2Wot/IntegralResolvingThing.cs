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
        private const string HrefPrefix = $"#{TDValues.HrefTitlePrefix}";

        private Dictionary<string, TDThing> titleToThingMap;
        private ErrorReporter errorReporter;

        public IntegralResolvingThing(TDThing thing, ErrorReporter errorReporter, Dictionary<string, TDThing> titleToThingMap)
        {
            this.titleToThingMap = titleToThingMap;
            this.errorReporter = errorReporter;
            ParsedThing = new ParsedThing(thing, string.Empty, string.Empty, new SchemaNamer(null), errorReporter, true, true);
        }

        public ParsedThing ParsedThing { get; }

        public bool TryResolve(string href, [NotNullWhen(true)] out IResolvingThing? resolvingThing)
        {
            string title = href.StartsWith(HrefPrefix) ? href.Substring(HrefPrefix.Length) : string.Empty;

            if (titleToThingMap.TryGetValue(title, out TDThing? referencedThing))
            {
                resolvingThing = new IntegralResolvingThing(referencedThing, errorReporter, titleToThingMap);
                return true;
            }

            resolvingThing = null;
            return false;
        }
    }
}
