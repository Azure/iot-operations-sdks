namespace Akri.Dtdl.Codegen
{
    using DTDLParser.Models;

    public partial class MapProto3 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string schema;
        private readonly DTSchemaInfo mapValueSchema;
        private readonly DtmiToSchemaName dtmiToSchemaName;
        private readonly HashSet<string> importNames;

        public MapProto3(string projectName, string genNamespace, string schema, DTSchemaInfo mapValueSchema, DtmiToSchemaName dtmiToSchemaName)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.mapValueSchema = mapValueSchema;
            this.dtmiToSchemaName = dtmiToSchemaName;
            this.importNames = new HashSet<string>();
        }

        public string FileName { get => $"{this.schema}.proto"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
