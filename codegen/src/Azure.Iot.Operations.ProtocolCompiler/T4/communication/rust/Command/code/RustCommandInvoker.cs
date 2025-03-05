
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustCommandInvoker : ITemplateTransform
    {
        private readonly CodeName commandName;
        private readonly CodeName genNamespace;
        private readonly EmptyTypeName serializerEmptyType;
        private readonly ITypeName? reqSchema;
        private readonly ITypeName? respSchema;
        private readonly CodeName? reqNamespace;
        private readonly CodeName? respNamespace;
        private readonly bool doesCommandTargetExecutor;

        public RustCommandInvoker(CodeName commandName, CodeName genNamespace, EmptyTypeName serializerEmptyType, ITypeName? reqSchema, ITypeName? respSchema, CodeName? reqNamespace, CodeName? respNamespace, bool doesCommandTargetExecutor)
        {
            this.commandName = commandName;
            this.genNamespace = genNamespace;
            this.serializerEmptyType = serializerEmptyType;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
            this.reqNamespace = reqNamespace;
            this.respNamespace = respNamespace;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.Rust, "command", "invoker")}.rs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Rust); }
    }
}
