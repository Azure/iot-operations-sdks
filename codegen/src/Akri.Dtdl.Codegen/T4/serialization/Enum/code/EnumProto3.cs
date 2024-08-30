namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;
    using DTDLParser;

    public partial class EnumProto3 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string schema;
        private readonly string valueSchema;
        private readonly List<(string, string, int)> nameValueIndices;
        private readonly (string, string, int) zeroNameValueIndex;

        public EnumProto3(string projectName, string genNamespace, string schema, Dtmi valueSchemaId, List<(string, string, int)> nameValueIndices)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.valueSchema = JsonSchemaSupport.GetPrimitiveType(valueSchemaId);
            this.nameValueIndices = nameValueIndices;
            this.zeroNameValueIndex = nameValueIndices.FirstOrDefault(nvi => nvi.Item2 == "0" || nvi.Item3 == 0, ($"{this.schema}_none", "0", 0));
        }

        public string FileName { get => $"{this.schema}.proto"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
