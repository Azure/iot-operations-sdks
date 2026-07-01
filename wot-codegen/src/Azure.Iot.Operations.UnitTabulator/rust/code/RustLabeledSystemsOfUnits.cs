// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System.Collections.Generic;

    public partial class RustLabeledSystemsOfUnits : ITemplateTransform
    {
        private readonly List<(string, string)> labeledSystemsOfUnits;

        public RustLabeledSystemsOfUnits(List<(string, string)> labeledSystemsOfUnits)
        {
            this.labeledSystemsOfUnits = labeledSystemsOfUnits;
        }

        public string FileName { get => "labeled_systems_of_units.rs"; }
    }
}
