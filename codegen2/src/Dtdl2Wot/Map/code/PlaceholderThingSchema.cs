// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Dtdl2Wot
{
    using DTDLParser.Models;

    public partial class PlaceholderThingSchema : ITemplateTransform
    {
        private readonly DTMapInfo dtMap;
        private readonly int indent;
        private readonly ThingDescriber thingDescriber;

        public PlaceholderThingSchema(DTMapInfo dtMap, int indent, ThingDescriber thingDescriber)
        {
            this.dtMap = dtMap;
            this.indent = indent;
            this.thingDescriber = thingDescriber;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
