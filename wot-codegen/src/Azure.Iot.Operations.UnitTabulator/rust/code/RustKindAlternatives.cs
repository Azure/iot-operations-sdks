// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System.Collections.Generic;

    public partial class RustKindAlternatives : ITemplateTransform
    {
        private readonly Dictionary<string, HashSet<string>> kindAlternativesMap;

        public RustKindAlternatives(Dictionary<string, HashSet<string>> kindAlternativesMap)
        {
            this.kindAlternativesMap = kindAlternativesMap;
        }

        public string FileName { get => "kind_alternatives.rs"; }
    }
}
