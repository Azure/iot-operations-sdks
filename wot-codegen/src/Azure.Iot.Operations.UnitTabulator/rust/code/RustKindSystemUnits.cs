// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System.Collections.Generic;
    using System.Linq;

    public partial class RustKindSystemUnits : ITemplateTransform
    {
        private readonly Dictionary<(string, string), string> kindSystemUnitsMap;

        public RustKindSystemUnits(Dictionary<(string, string), string> kindSystemUnitsMap)
        {
            this.kindSystemUnitsMap = kindSystemUnitsMap;
        }

        public string FileName { get => "kind_system_units.rs"; }
    }
}
