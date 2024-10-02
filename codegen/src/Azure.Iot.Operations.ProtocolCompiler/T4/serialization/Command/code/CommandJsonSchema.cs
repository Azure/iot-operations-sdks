namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class CommandJsonSchema : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly string intfBareId;
        private readonly string interfaceIdAsNamespace;
        private readonly string versionSuffix;
        private readonly string normalizedVersionSuffix;
        private readonly string schema;
        private readonly string commandName;
        private readonly string subType;
        private readonly string paramName;
        private readonly DTSchemaInfo paramSchema;
        private readonly bool isNullable;
        private readonly DtmiToSchemaName dtmiToSchemaName;
        private readonly bool setIndex;

        public CommandJsonSchema(string genNamespace, string interfaceId, string schema, string commandName, string subType, string paramName, DTSchemaInfo paramSchema, bool isNullable, DtmiToSchemaName dtmiToSchemaName, bool setIndex, string interfaceIdAsNamespace, string normalizedVersionSuffix)
        {
            int semiIndex = interfaceId.IndexOf(';');
            int bareIdLength = semiIndex >= 0 ? semiIndex : interfaceId.Length;

            this.intfBareId = interfaceId.Substring(0, bareIdLength);
            this.versionSuffix = interfaceId.Substring(bareIdLength);
            this.interfaceIdAsNamespace = interfaceIdAsNamespace;
            this.normalizedVersionSuffix = normalizedVersionSuffix;

            this.genNamespace = genNamespace;
            this.schema = schema;
            this.commandName = commandName;
            this.subType = subType;
            this.paramName = paramName;
            this.paramSchema = paramSchema;
            this.isNullable = isNullable;
            this.dtmiToSchemaName = dtmiToSchemaName;
            this.setIndex = setIndex;
        }

        public string FileName { get => $"{this.schema}.schema.json"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
