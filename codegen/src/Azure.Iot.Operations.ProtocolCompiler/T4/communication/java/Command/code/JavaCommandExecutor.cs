
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class JavaCommandExecutor : ITemplateTransform
    {
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly CodeName genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string? reqSchema;
        private readonly string? respSchema;

        public JavaCommandExecutor(CodeName commandName, CodeName genNamespace, string serializerSubNamespace, string serializerClassName, string? reqSchema, string? respSchema)
        {
            this.commandName = commandName.AsGiven;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName.AsGiven[0]) + commandName.AsGiven.Substring(1);
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = char.ToUpperInvariant(serializerSubNamespace[0]) + serializerSubNamespace.Substring(1);
            this.serializerClassName = serializerClassName;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
        }

        public string FileName { get => $"{this.capitalizedCommandName}CommandExecutor.java"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Java); }
    }
}
