namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustAggregateError : IEnvoyTemplateTransform
    {
        private readonly CodeName schemaName;
        private readonly CodeName genNamespace;
        private readonly List<(CodeName, CodeName)> innerNameSchemas;
        private readonly string srcSubdir;

        public RustAggregateError(CodeName schemaName, CodeName genNamespace, List<(CodeName, CodeName)> innerNameSchemas, string srcSubdir)
        {
            this.schemaName = schemaName;
            this.genNamespace = genNamespace;
            this.innerNameSchemas = innerNameSchemas;
            this.srcSubdir = srcSubdir;
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.Rust, "error")}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Shared; }
    }
}
