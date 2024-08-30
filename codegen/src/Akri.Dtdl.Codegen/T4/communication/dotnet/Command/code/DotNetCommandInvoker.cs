
namespace Akri.Dtdl.Codegen
{
    public partial class DotNetCommandInvoker : ITemplateTransform
    {
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string serviceName;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string serialzerEmptyType;
        private readonly string? reqSchema;
        private readonly string? respSchema;

        public DotNetCommandInvoker(string commandName, string projectName, string genNamespace, string serviceName, string serializerSubNamespace, string serializerClassName, string serialzerEmptyType, string? reqSchema, string? respSchema)
        {
            this.commandName = commandName;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName[0]) + commandName.Substring(1);
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = serializerClassName;
            this.serialzerEmptyType = serialzerEmptyType == "" ? "byte[]" : serialzerEmptyType;
            this.reqSchema = reqSchema == "" ? "byte[]" : reqSchema;
            this.respSchema = respSchema == "" ? "byte[]" : respSchema;
        }

        public string FileName { get => $"{this.capitalizedCommandName}CommandInvoker.g.cs"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
