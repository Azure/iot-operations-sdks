// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2Wot
{
    using System.Diagnostics.CodeAnalysis;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    public class NullResolvingThing : IResolvingThing
    {
        public NullResolvingThing(TDThing thing, ErrorReporter errorReporter)
        {
            ParsedThing = new ParsedThing(thing, string.Empty, string.Empty, new SchemaNamer(null), errorReporter, true, true);
        }

        public ParsedThing ParsedThing { get; }

        public bool TryResolve(string href, [NotNullWhen(true)] out IResolvingThing? resolvingThing)
        {
            resolvingThing = null;
            return false;
        }
    }
}
