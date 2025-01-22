
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustCommandInvoker : ITemplateTransform
    {
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly string genNamespace;
        private readonly string serializerEmptyType;
        private readonly string? reqSchema;
        private readonly string? respSchema;
        private readonly bool doesCommandTargetExecutor;

        public RustCommandInvoker(string commandName, string genNamespace, string serializerEmptyType, string? reqSchema, string? respSchema, bool doesCommandTargetExecutor)
        {
            this.commandName = commandName;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName[0]) + commandName.Substring(1);
            this.genNamespace = genNamespace;
            this.serializerEmptyType = serializerEmptyType == "" ? "byte[]" : serializerEmptyType;
            this.reqSchema = reqSchema == "" ? "Bytes" : reqSchema;
            this.respSchema = respSchema == "" ? "Bytes" : respSchema;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.capitalizedCommandName}CommandInvoker.rs"); }

        public string FolderPath { get => this.genNamespace; }
    }
}
