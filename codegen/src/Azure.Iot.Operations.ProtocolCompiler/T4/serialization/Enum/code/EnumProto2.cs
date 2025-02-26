namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using DTDLParser;

    public partial class EnumProto2 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName schema;
        private readonly string valueSchema;
        private readonly List<(string, string, int)> nameValueIndices;
        private readonly (string, string, int) zeroNameValueIndex;

        public EnumProto2(string projectName, CodeName genNamespace, CodeName schema, Dtmi valueSchemaId, List<(string, string, int)> nameValueIndices)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.valueSchema = JsonSchemaSupport.GetPrimitiveType(valueSchemaId);
            this.nameValueIndices = nameValueIndices;
            this.zeroNameValueIndex = nameValueIndices.FirstOrDefault(nvi => nvi.Item2 == "0" || nvi.Item3 == 0, ($"{this.schema.AsGiven}_none", "0", 0));
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.proto"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Independent); }
    }
}
