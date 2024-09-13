namespace Akri.Dtdl.Codegen
{
    using DTDLParser.Models;

    public partial class CommandProto2 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string schema;
        private readonly string paramName;
        private readonly DTSchemaInfo paramSchema;
        private readonly bool isNullable;
        private readonly DtmiToSchemaName dtmiToSchemaName;
        private readonly HashSet<string> importNames;

        public CommandProto2(string projectName, string genNamespace, string schema, string paramName, DTSchemaInfo paramSchema, bool isNullable, DtmiToSchemaName dtmiToSchemaName)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.paramName = paramName;
            this.paramSchema = paramSchema;
            this.isNullable = isNullable;
            this.dtmiToSchemaName = dtmiToSchemaName;
            this.importNames = new HashSet<string>();
        }

        public string FileName { get => $"{this.schema}.proto"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
