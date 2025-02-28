
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class PythonCommandExecutor : ITemplateTransform
    {
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly CodeName genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string reqSchema;
        private readonly string respSchema;

        public PythonCommandExecutor(CodeName commandName, CodeName genNamespace, string serializerSubNamespace, string serializerClassName, string? reqSchema, string? respSchema)
        {
            this.commandName = commandName.AsGiven;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName.AsGiven[0]) + commandName.AsGiven.Substring(1);
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = string.Format(serializerClassName, string.Empty);
            this.reqSchema = reqSchema == "" ? "any" : reqSchema ?? "None";
            this.respSchema = respSchema == "" ? "any" : respSchema ?? "None";
        }

        public string FileName { get => $"{this.capitalizedCommandName}CommandExecutor_g.py"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Python); }
    }
}
