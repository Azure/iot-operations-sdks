// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System.Collections.Generic;

    public partial class RustUnitInfos : ITemplateTransform
    {
        private readonly Dictionary<string, UnitInfo> unitInfosMap;

        public RustUnitInfos(Dictionary<string, UnitInfo> unitInfosMap)
        {
            this.unitInfosMap = unitInfosMap;
        }

        public string FileName { get => "unit_infos.rs"; }

        private string ToRustString(double val)
        {
            string str = val.ToString("r", System.Globalization.CultureInfo.InvariantCulture);
            return !str.Contains(".") && !str.Contains("e") ? $"{str}.0" : str;
        }
    }
}
