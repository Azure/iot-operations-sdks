
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class GoCommandExecutor : ITemplateTransform
    {
        private readonly CodeName commandName;
        private readonly CodeName genNamespace;
        private readonly string serializerSubNamespace;
        private readonly ITypeName? reqSchema;
        private readonly ITypeName? respSchema;
        private readonly bool isIdempotent;
        private readonly string? ttl;

        public GoCommandExecutor(CodeName commandName, CodeName genNamespace, string serializerSubNamespace, ITypeName? reqSchema, ITypeName? respSchema, bool isIdempotent, string? ttl)
        {
            this.commandName = commandName;
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
            this.isIdempotent = isIdempotent;
            this.ttl = ttl;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.Go, "command", "executor")}.go"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Go); }
    }
}
