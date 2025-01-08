
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustCommandExecutor : ITemplateTransform
    {
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly string genNamespace;
        private readonly string serializerEmptyType;
        private readonly string? reqSchema;
        private readonly string? respSchema;
        private readonly bool isIdempotent;
        private readonly string? ttl;
        private readonly bool useSharedSubscription;

        public RustCommandExecutor(string commandName, string genNamespace, string serializerEmptyType, string? reqSchema, string? respSchema, bool isIdempotent, string? ttl, bool useSharedSubscription)
        {
            this.commandName = commandName;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName[0]) + commandName.Substring(1);
            this.genNamespace = genNamespace;
            this.serializerEmptyType = serializerEmptyType == "" ? "byte[]" : serializerEmptyType;
            this.reqSchema = reqSchema == "" ? "Bytes" : reqSchema;
            this.respSchema = respSchema == "" ? "Bytes" : respSchema;
            this.isIdempotent = isIdempotent;
            this.ttl = ttl;
            this.useSharedSubscription = useSharedSubscription;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.capitalizedCommandName}CommandExecutor.rs"); }

        public string FolderPath { get => this.genNamespace; }
    }
}
