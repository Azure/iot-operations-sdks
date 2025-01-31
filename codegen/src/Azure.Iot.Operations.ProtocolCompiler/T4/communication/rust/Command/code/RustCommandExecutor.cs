
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustCommandExecutor : ITemplateTransform
    {
        private readonly CodeName commandName;
        private readonly CodeName genNamespace;
        private readonly EmptyTypeName serializerEmptyType;
        private readonly ITypeName? reqSchema;
        private readonly ITypeName? respSchema;
        private readonly bool isIdempotent;
        private readonly string? ttl;
        private readonly bool useSharedSubscription;

        public RustCommandExecutor(CodeName commandName, CodeName genNamespace, EmptyTypeName serializerEmptyType, ITypeName? reqSchema, ITypeName? respSchema, bool isIdempotent, string? ttl, bool useSharedSubscription)
        {
            this.commandName = commandName;
            this.genNamespace = genNamespace;
            this.serializerEmptyType = serializerEmptyType;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
            this.isIdempotent = isIdempotent;
            this.ttl = ttl;
            this.useSharedSubscription = useSharedSubscription;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.Rust, "command", "executor")}.rs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Rust); }
    }
}
