
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class GoCommandInvoker : ITemplateTransform
    {
        private readonly CodeName commandName;
        private readonly CodeName genNamespace;
        private readonly string serializerSubNamespace;
        private readonly ITypeName? reqSchema;
        private readonly ITypeName? respSchema;
        private readonly bool doesCommandTargetExecutor;

        public GoCommandInvoker(CodeName commandName, CodeName genNamespace, string serializerSubNamespace, ITypeName? reqSchema, ITypeName? respSchema, bool doesCommandTargetExecutor)
        {
            this.commandName = commandName;
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.Go, "command", "invoker")}.go"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Go); }
    }
}
