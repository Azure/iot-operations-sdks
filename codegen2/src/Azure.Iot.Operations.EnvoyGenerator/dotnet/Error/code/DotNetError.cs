// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.EnvoyGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetError : IEnvoyTemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName schemaName;
        private readonly CodeName genNamespace;
        private readonly CodeName? errorCodeName;
        private readonly CodeName? errorCodeSchema;
        private readonly CodeName? errorInfoName;
        private readonly CodeName? errorInfoSchema;
        private readonly string description;
        private readonly CodeName? messageField;
        private readonly bool messageIsRequired;

        public DotNetError(string projectName, CodeName schemaName, CodeName genNamespace, CodeName? errorCodeName, CodeName? errorCodeSchema, CodeName? errorInfoName, CodeName? errorInfoSchema, string description, CodeName? messageField, bool messageIsRequired)
        {
            this.projectName = projectName;
            this.schemaName = schemaName;
            this.genNamespace = genNamespace;
            this.errorCodeName = errorCodeName;
            this.errorCodeSchema = errorCodeSchema;
            this.errorInfoName = errorInfoName;
            this.errorInfoSchema = errorInfoSchema;
            this.description = description;
            this.messageField = messageField;
            this.messageIsRequired = messageIsRequired;
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.CSharp, "exception")}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Shared; }
    }
}
