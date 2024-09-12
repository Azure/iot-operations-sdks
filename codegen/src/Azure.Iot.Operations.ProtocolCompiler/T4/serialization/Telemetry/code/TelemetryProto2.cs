namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class TelemetryProto2 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string schema;
        private readonly List<(string, string, DTSchemaInfo, int)> nameDescSchemaIndices;
        private readonly HashSet<DTSchemaInfo> uniqueSchemas;
        private readonly DtmiToSchemaName dtmiToSchemaName;
        private readonly HashSet<string> importNames;

        public TelemetryProto2(string projectName, string genNamespace, string schema, List<(string, string, DTSchemaInfo, int)> nameDescSchemaIndices, DtmiToSchemaName dtmiToSchemaName)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.nameDescSchemaIndices = nameDescSchemaIndices;
            this.uniqueSchemas = new HashSet<DTSchemaInfo>(nameDescSchemaIndices.Select(ndsi => ndsi.Item3));
            this.dtmiToSchemaName = dtmiToSchemaName;
            this.importNames = new HashSet<string>();
        }

        public string FileName { get => $"{this.schema}.proto"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
