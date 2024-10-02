
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class GoCommandExecutor : ITemplateTransform
    {
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly string genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string? reqSchema;
        private readonly string? respSchema;
        private readonly bool isIdempotent;
        private readonly string? ttl;

        public GoCommandExecutor(string commandName, string genNamespace, string serializerSubNamespace, string? reqSchema, string? respSchema, bool isIdempotent, string? ttl)
        {
            this.commandName = commandName;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName[0]) + commandName.Substring(1);
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
            this.isIdempotent = isIdempotent;
            this.ttl = ttl;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.capitalizedCommandName}CommandExecutor.go"); }

        public string FolderPath { get => this.genNamespace; }
    }
}
