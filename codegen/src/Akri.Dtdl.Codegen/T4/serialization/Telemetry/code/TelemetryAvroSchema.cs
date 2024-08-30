namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class TelemetryAvroSchema : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string schema;
        private readonly List<(string, string, DTSchemaInfo, int)> nameDescSchemaIndices;
        private readonly DtmiToSchemaName dtmiToSchemaName;

        public TelemetryAvroSchema(string projectName, string genNamespace, string schema, List<(string, string, DTSchemaInfo, int)> nameDescSchemaIndices, DtmiToSchemaName dtmiToSchemaName)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.nameDescSchemaIndices = nameDescSchemaIndices;
            this.dtmiToSchemaName = dtmiToSchemaName;
        }

        public string FileName { get => $"{this.schema}.avsc"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
