namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustConstants : IEnvoyTemplateTransform
    {
        private readonly CodeName schemaName;
        private readonly CodeName genNamespace;
        private readonly List<TypedConstant> constants;
        private readonly string srcSubdir;

        public RustConstants(CodeName schemaName, CodeName genNamespace, List<TypedConstant> constants, string srcSubdir)
        {
            this.schemaName = schemaName;
            this.genNamespace = genNamespace;
            this.constants = constants;
            this.srcSubdir = srcSubdir;
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }

        private static string GetRustType(string type)
        {
            return type switch
            {
                TDValues.TypeString => "&str",
                TDValues.TypeNumber => "f64",
                TDValues.TypeInteger => "i32",
                _ => throw new System.ArgumentException($"Unsupported constant type: {type}"),
            };
        }
    }
}
