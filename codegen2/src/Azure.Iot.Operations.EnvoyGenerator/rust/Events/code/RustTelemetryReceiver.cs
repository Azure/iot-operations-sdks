// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustTelemetryReceiver : IEnvoyTemplateTransform
    {
        private readonly CodeName telemetryName;
        private readonly CodeName componentName;
        private readonly MultiCodeName genNamespace;
        private readonly MultiCodeName commonNs;
        private readonly ITypeName schemaType;
        private readonly CodeName messageName;
        private readonly string? serviceGroupId;
        private readonly string topicPattern;
        private readonly string srcSubdir;

        public RustTelemetryReceiver(string telemetryName, string componentName, MultiCodeName genNamespace, MultiCodeName commonNs, ITypeName schemaType, string? serviceGroupId, string topicPattern, string messageStub, string srcSubdir)
        {
            this.telemetryName = new CodeName(telemetryName);
            this.componentName = new CodeName(componentName);
            this.genNamespace = genNamespace;
            this.commonNs = commonNs;
            this.schemaType = schemaType;
            this.messageName = new CodeName(messageStub, "message");
            this.serviceGroupId = serviceGroupId;
            this.topicPattern = topicPattern;
            this.srcSubdir = srcSubdir;
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Client; }
    }
}
