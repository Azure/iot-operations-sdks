
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class DotNetCommandInvoker : ITemplateTransform
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
        private readonly CodeName? reqNamespace;
        private readonly CodeName? respNamespace;

        public DotNetCommandInvoker(CodeName commandName, string projectName, CodeName genNamespace, string modelId, CodeName serviceName, string serializerSubNamespace, string serializerClassName, EmptyTypeName serializerEmptyType, ITypeName? reqSchema, ITypeName? respSchema, CodeName? reqNamespace, CodeName? respNamespace)
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
            this.reqNamespace = reqNamespace;
            this.respNamespace = respNamespace;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.CSharp, "command", "invoker")}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
