// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetObject : ITypeTemplateTransform
    {
        private readonly string projectName;
        private readonly MultiCodeName genNamespace;
        private readonly MultiCodeName commonNs;
        private readonly ObjectType objectType;
        private readonly SerializationFormat serFormat;
        private readonly bool needsNullCheck;

        internal DotNetObject(string projectName, MultiCodeName genNamespace, MultiCodeName commonNs, ObjectType objectType, SerializationFormat serFormat)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.commonNs = commonNs;
            this.objectType = objectType;
            this.serFormat = serFormat;
            this.needsNullCheck = objectType.FieldInfos.Any(fi => !fi.Value.SchemaType.OrNull && DotNetSchemaSupport.IsNullable(fi.Value.SchemaType));
        }

        public string FileName { get => $"{this.objectType.SchemaName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
