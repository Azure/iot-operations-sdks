
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class DotNetCommandExecutor : ITemplateTransform
    {
        private readonly CodeName commandName;
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly string modelId;
        private readonly CodeName serviceName;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly EmptyTypeName serializerEmptyType;
        private readonly ITypeName? reqSchema;
        private readonly ITypeName? respSchema;
        private readonly bool isIdempotent;
        private readonly string? ttl;

        public DotNetCommandExecutor(CodeName commandName, string projectName, CodeName genNamespace, string modelId, CodeName serviceName, string serializerSubNamespace, string serializerClassName, EmptyTypeName serializerEmptyType, ITypeName? reqSchema, ITypeName? respSchema, bool isIdempotent, string? ttl)
        {
            this.commandName = commandName;
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = serializerClassName;
            this.serializerEmptyType = serializerEmptyType;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
            this.isIdempotent = isIdempotent;
            this.ttl = ttl;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.CSharp, "command", "executor")}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
