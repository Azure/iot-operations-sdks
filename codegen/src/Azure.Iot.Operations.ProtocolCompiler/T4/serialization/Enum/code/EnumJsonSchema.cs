namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using DTDLParser;

    public partial class EnumJsonSchema : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly string objBareId;
        private readonly string versionSuffix;
        private readonly string description;
        private readonly string schema;
        private readonly string valueSchema;
        private readonly List<(string, string, int)> nameValueIndices;

        public EnumJsonSchema(string genNamespace, string objectId, string description, string schema, Dtmi valueSchemaId, List<(string, string, int)> nameValueIndices)
        {
            int semiIndex = objectId.IndexOf(';');
            int bareIdLength = semiIndex >= 0 ? semiIndex : objectId.Length;

            this.objBareId = objectId.Substring(0, bareIdLength);
            this.versionSuffix = objectId.Substring(bareIdLength);

            this.genNamespace = genNamespace;
            this.description = description;
            this.schema = schema;
            this.valueSchema = JsonSchemaSupport.GetPrimitiveType(valueSchemaId);
            this.nameValueIndices = nameValueIndices;
        }

        public string FileName { get => $"{this.schema}.schema.json"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
