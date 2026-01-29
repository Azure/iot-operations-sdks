// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetAggregateError : IEnvoyTemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName schemaName;
        private readonly MultiCodeName schemaNamespace;
        private readonly List<(CodeName, CodeName)> innerNameSchemas;

        public DotNetAggregateError(string projectName, CodeName schemaName, MultiCodeName schemaNamespace, List<(CodeName, CodeName)> innerNameSchemas)
        {
            this.projectName = projectName;
            this.schemaName = schemaName;
            this.schemaNamespace = schemaNamespace;
            this.innerNameSchemas = innerNameSchemas;
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.CSharp, "exception")}.g.cs"; }

        public string FolderPath { get => this.schemaNamespace.GetFolderName(TargetLanguage.CSharp); }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Shared; }
    }
}
