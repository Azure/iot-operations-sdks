// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System.Collections.Generic;

    public partial class EceCodes : ITemplateTransform
    {
        private readonly Dictionary<string, string> eceCodesMap;

        public EceCodes(Dictionary<string, string> eceCodesMap)
        {
            this.eceCodesMap = eceCodesMap;
        }

        public string FileName { get => "ece_codes.rs"; }
    }
}
