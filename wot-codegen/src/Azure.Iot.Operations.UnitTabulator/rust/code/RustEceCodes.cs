// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System.Collections.Generic;

    public partial class RustEceCodes : ITemplateTransform
    {
        private readonly Dictionary<string, string> eceCodesMap;

        public RustEceCodes(Dictionary<string, string> eceCodesMap)
        {
            this.eceCodesMap = eceCodesMap;
        }

        public string FileName { get => "ece_codes.rs"; }
    }
}
