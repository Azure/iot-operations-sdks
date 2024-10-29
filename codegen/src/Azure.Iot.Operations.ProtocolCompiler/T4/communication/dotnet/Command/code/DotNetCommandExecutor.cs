
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class DotNetCommandExecutor : ITemplateTransform
    {
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string modelId;
        private readonly string serviceName;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string serializerEmptyType;
        private readonly string? reqSchema;
        private readonly string? respSchema;
        private readonly bool isIdempotent;
        private readonly string? ttl;

        public DotNetCommandExecutor(string commandName, string projectName, string genNamespace, string modelId, string serviceName, string serializerSubNamespace, string serializerClassName, string serializerEmptyType, string? reqSchema, string? respSchema, bool isIdempotent, string? ttl)
        {
            this.commandName = commandName;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName[0]) + commandName.Substring(1);
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = serializerClassName;
            this.serializerEmptyType = serializerEmptyType == "" ? "byte[]" : serializerEmptyType;
            this.reqSchema = reqSchema == "" ? "byte[]" : reqSchema;
            this.respSchema = respSchema == "" ? "byte[]" : respSchema;
            this.isIdempotent = isIdempotent;
            this.ttl = ttl;
        }

        public string FileName { get => $"{this.capitalizedCommandName}CommandExecutor.g.cs"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
