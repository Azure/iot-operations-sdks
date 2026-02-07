// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustAlias : ITypeTemplateTransform
    {
        private readonly MultiCodeName genNamespace;
        private readonly AliasType aliasType;
        private readonly string srcSubdir;

        internal RustAlias(MultiCodeName genNamespace, AliasType aliasType, string srcSubdir)
        {
            this.genNamespace = genNamespace;
            this.aliasType = aliasType;
            this.srcSubdir = srcSubdir;
        }

        public string FileName { get => $"{this.aliasType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
