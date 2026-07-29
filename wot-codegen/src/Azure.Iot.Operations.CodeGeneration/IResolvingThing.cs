// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Diagnostics.CodeAnalysis;
    using Azure.Iot.Operations.TDParser.Model;

    public interface IResolvingThing
    {
        ParsedThing ParsedThing { get; }

        bool TryResolve(string href, [NotNullWhen(true)] out IResolvingThing? resolvingThing);
    }
}
