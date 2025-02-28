namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class ArrayProto2 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName schema;
        private readonly DTSchemaInfo elementSchema;
        private readonly HashSet<string> importNames;

        public ArrayProto2(string projectName, CodeName genNamespace, CodeName schema, DTSchemaInfo elementSchema)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.elementSchema = elementSchema;
            this.importNames = new HashSet<string>();
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.proto"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Independent); }
    }
}
