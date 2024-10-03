
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class PythonCommandInvoker : ITemplateTransform
    {
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly string genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string reqSchema;
        private readonly string respSchema;

        public PythonCommandInvoker(string commandName, string genNamespace, string serializerSubNamespace, string serializerClassName, string? reqSchema, string? respSchema)
        {
            this.commandName = commandName;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName[0]) + commandName.Substring(1);
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = string.Format(serializerClassName, string.Empty);
            this.reqSchema = reqSchema == "" ? "any" : reqSchema ?? "None";
            this.respSchema = respSchema == "" ? "any" : respSchema ?? "None";
        }

        public string FileName { get => $"{this.capitalizedCommandName}CommandInvoker_g.py"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
