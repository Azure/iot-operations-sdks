namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public partial class AggregateResponseJsonSchema : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly ITypeName schema;
        private readonly CodeName errorSchema;
        private readonly bool includeValue;
        private readonly bool setIndex;

        public AggregateResponseJsonSchema(CodeName genNamespace, ITypeName schema, CodeName errorSchema, bool includeValue, bool setIndex)
        {
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.errorSchema = errorSchema;
            this.includeValue = includeValue;
            this.setIndex = setIndex;
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.schema.json"; }

        public string FolderPath { get => this.genNamespace.GetFileName(TargetLanguage.Independent); }
    }
}
