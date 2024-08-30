
namespace Akri.Dtdl.Codegen
{
    public partial class JavaCommandInvoker : ITemplateTransform
    {
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly string genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string? reqSchema;
        private readonly string? respSchema;

        public JavaCommandInvoker(string commandName, string genNamespace, string serializerSubNamespace, string serializerClassName, string? reqSchema, string? respSchema)
        {
            this.commandName = commandName;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName[0]) + commandName.Substring(1);
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = char.ToUpperInvariant(serializerSubNamespace[0]) + serializerSubNamespace.Substring(1);
            this.serializerClassName = serializerClassName;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
        }

        public string FileName { get => $"{this.capitalizedCommandName}CommandInvoker.java"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
