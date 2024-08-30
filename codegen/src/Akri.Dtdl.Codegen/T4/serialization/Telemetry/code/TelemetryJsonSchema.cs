namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class TelemetryJsonSchema : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly string intfBareId;
        private readonly string versionSuffix;
        private readonly string schema;
        private readonly List<(string, string, DTSchemaInfo, int)> nameDescSchemaIndices;
        private readonly DtmiToSchemaName dtmiToSchemaName;
        private readonly bool setIndex;

        public TelemetryJsonSchema(string genNamespace, string interfaceId, string schema, List<(string, string, DTSchemaInfo, int)> nameDescSchemaIndices, DtmiToSchemaName dtmiToSchemaName, bool setIndex)
        {
            int semiIndex = interfaceId.IndexOf(';');
            int bareIdLength = semiIndex >= 0 ? semiIndex : interfaceId.Length;

            this.intfBareId = interfaceId.Substring(0, bareIdLength);
            this.versionSuffix = interfaceId.Substring(bareIdLength);

            this.genNamespace = genNamespace;
            this.schema = schema;
            this.nameDescSchemaIndices = nameDescSchemaIndices;
            this.dtmiToSchemaName = dtmiToSchemaName;
            this.setIndex = setIndex;
        }

        public string FileName { get => $"{this.schema}.schema.json"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
