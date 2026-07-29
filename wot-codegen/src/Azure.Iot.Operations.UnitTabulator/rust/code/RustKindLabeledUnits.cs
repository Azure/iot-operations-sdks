// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System.Collections.Generic;

    public partial class RustKindLabeledUnits : ITemplateTransform
    {
        private readonly Dictionary<string, List<(string, string)>> kindLabeledUnitsMap;

        public RustKindLabeledUnits(Dictionary<string, List<(string, string)>> kindLabeledUnitsMap)
        {
            this.kindLabeledUnitsMap = kindLabeledUnitsMap;
        }

        public string FileName { get => "kind_labeled_units.rs"; }
    }
}
