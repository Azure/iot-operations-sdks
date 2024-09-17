
namespace Akri.Dtdl.Codegen
{
    public partial class RustCommandInvoker : ITemplateTransform
    {
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly string genNamespace;
        private readonly string serialzerEmptyType;
        private readonly string? reqSchema;
        private readonly string? respSchema;

        public RustCommandInvoker(string commandName, string genNamespace, string serialzerEmptyType, string? reqSchema, string? respSchema)
        {
            this.commandName = commandName;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName[0]) + commandName.Substring(1);
            this.genNamespace = genNamespace;
            this.serialzerEmptyType = serialzerEmptyType == "" ? "byte[]" : serialzerEmptyType;
            this.reqSchema = reqSchema == "" ? "Bytes" : reqSchema;
            this.respSchema = respSchema == "" ? "Bytes" : respSchema;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.capitalizedCommandName}CommandInvoker.rs"); }

        public string FolderPath { get => Path.Combine(SubPaths.Rust, this.genNamespace); }
    }
}
