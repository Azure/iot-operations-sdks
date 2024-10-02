namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class TelemetryProto3 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string schema;
        private readonly List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices;
        private readonly HashSet<DTSchemaInfo> uniqueSchemas;
        private readonly DtmiToSchemaName dtmiToSchemaName;
        private readonly HashSet<string> importNames;

        public TelemetryProto3(string projectName, string genNamespace, string schema, List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices, DtmiToSchemaName dtmiToSchemaName)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.nameDescSchemaRequiredIndices = nameDescSchemaRequiredIndices;
            this.uniqueSchemas = new HashSet<DTSchemaInfo>(nameDescSchemaRequiredIndices.Select(ndsi => ndsi.Item3));
            this.dtmiToSchemaName = dtmiToSchemaName;
            this.importNames = new HashSet<string>();
        }

        public string FileName { get => $"{this.schema}.proto"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
