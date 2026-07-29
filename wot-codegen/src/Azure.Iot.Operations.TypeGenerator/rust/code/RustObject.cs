// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustObject : ITypeTemplateTransform
    {
        private readonly MultiCodeName genNamespace;
        private readonly MultiCodeName commonNs;
        private readonly ObjectType objectType;
        private readonly IReadOnlyCollection<ReferenceType> referencedSchemas;
        private readonly bool allowSkipping;
        private readonly string srcSubdir;

        internal RustObject(MultiCodeName genNamespace, MultiCodeName commonNs, ObjectType objectType, bool allowSkipping, string srcSubdir)
        {
            this.genNamespace = genNamespace;
            this.commonNs = commonNs;
            this.objectType = objectType;
            this.referencedSchemas = TypeGeneratorSupport.GetReferencedSchemas(objectType);
            this.allowSkipping = allowSkipping;
            this.srcSubdir = srcSubdir;
        }

        public string FileName { get => $"{this.objectType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
