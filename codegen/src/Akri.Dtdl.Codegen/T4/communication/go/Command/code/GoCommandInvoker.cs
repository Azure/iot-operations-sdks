
namespace Akri.Dtdl.Codegen
{
    public partial class GoCommandInvoker : ITemplateTransform
    {
        private readonly string commandName;
        private readonly string capitalizedCommandName;
        private readonly string genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string? reqSchema;
        private readonly string? respSchema;
        private readonly bool doesCommandTargetExecutor;

        public GoCommandInvoker(string commandName, string genNamespace, string serializerSubNamespace, string? reqSchema, string? respSchema, bool doesCommandTargetExecutor)
        {
            this.commandName = commandName;
            this.capitalizedCommandName = char.ToUpperInvariant(commandName[0]) + commandName.Substring(1);
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.capitalizedCommandName}CommandInvoker.go"); }

        public string FolderPath { get => this.genNamespace; }
    }
}
