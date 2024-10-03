using Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor;

namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.T4
{
    public partial class DotNetEnumTokenizer : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly string serviceName;
        private readonly string testName;
        private readonly EnumTypeInfo enumSchema;

        public DotNetEnumTokenizer(string genNamespace, string serviceName, string testName, EnumTypeInfo enumSchema)
        {
            this.genNamespace = genNamespace;
            this.serviceName = serviceName;
            this.testName = testName;
            this.enumSchema = enumSchema;

            foreach (string enumName in this.enumSchema.EnumNames) { }
        }

        public string FileName { get => $"{this.enumSchema.SchemaName}_Tokenizer.g.cs"; }

        public string FolderPath { get => $"dotnet\\library\\{this.genNamespace}"; }
    }
}
