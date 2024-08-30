
namespace Akri.Dtdl.Codegen
{
    public partial class CCommandInvoker : ITemplateTransform
    {
        private readonly string modelId;
        private readonly string? normalizedVersionSuffix;
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly string requestTopicName;
        private readonly string genNamespace;
        private readonly string serviceName;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string? reqSchema;
        private readonly string? respSchema;

        private const string RequestTopicSuffix = "clients/{invokerClientId}/";

        public CCommandInvoker(string modelId, string commandName, string requestTopicName, string genNamespace, string serviceName, string serializerSubNamespace, string serializerClassName, string? reqSchema, string? respSchema, string? normalizedVersionSuffix)
        {
            this.modelId = modelId;
            this.normalizedVersionSuffix = normalizedVersionSuffix;
            this.commandName = commandName;
            this.requestTopicName = requestTopicName;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName[0]) + commandName.Substring(1);
            this.genNamespace = genNamespace;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = serializerClassName;
            this.reqSchema = reqSchema == "" ? "byte[]" : reqSchema;
            this.respSchema = respSchema == "" ? "byte[]" : respSchema;
        }

        public string FileName { get => $"{GetFullyQualifiedName().ToLower()}_invoker.h"; }

        public string FolderPath { get => this.genNamespace; }

        private string GetFullyQualifiedServiceName() =>
            this.normalizedVersionSuffix != null ?
                $"{this.serviceName}_{this.normalizedVersionSuffix}" :
                $"{this.serviceName}";

        private string GetFullyQualifiedName() =>
            (this.normalizedVersionSuffix != null ?
                $"{this.serviceName}_{this.commandName}_{this.normalizedVersionSuffix}" :
                $"{this.serviceName}_{this.commandName}");

        private string GetRequestTopicFormat() => this.requestTopicName;

        private string GetResponseTopicFormat() => $"{RequestTopicSuffix}{this.requestTopicName}";
    }
}
