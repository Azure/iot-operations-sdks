// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetEnum : ITypeTemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly EnumType enumType;

        internal DotNetEnum(string projectName, CodeName genNamespace, EnumType enumType)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.enumType = enumType;
        }

        public string FileName { get => $"{this.enumType.SchemaName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
