namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class CommandAvroSchema : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string schema;
        private readonly string commandName;
        private readonly string subType;
        private readonly string paramName;
        private readonly DTSchemaInfo paramSchema;
        private readonly bool isNullable;
        private readonly DtmiToSchemaName dtmiToSchemaName;

        public CommandAvroSchema(string projectName, string genNamespace, string schema, string commandName, string subType, string paramName, DTSchemaInfo paramSchema, bool isNullable, DtmiToSchemaName dtmiToSchemaName)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.commandName = commandName;
            this.subType = subType;
            this.paramName = paramName;
            this.paramSchema = paramSchema;
            this.isNullable = isNullable;
            this.dtmiToSchemaName = dtmiToSchemaName;
        }

        public string FileName { get => $"{this.schema}.avsc"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
