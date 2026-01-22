// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    using System.IO;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustEnum : ITypeTemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly EnumType enumType;
        private readonly string srcSubdir;
        private readonly bool hasNonPascalNames;

        internal RustEnum(CodeName genNamespace, EnumType enumType, string srcSubdir)
        {
            this.genNamespace = genNamespace;
            this.enumType = enumType;
            this.srcSubdir = srcSubdir;
            this.hasNonPascalNames = this.enumType.EnumValues.Any(v => !char.IsUpper(v.AsGiven[0]) || v.AsGiven.Contains('_'));
        }

        public string FileName { get => $"{this.enumType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
