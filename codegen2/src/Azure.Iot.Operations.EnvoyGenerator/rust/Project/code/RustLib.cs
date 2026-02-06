// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustLib : IEnvoyTemplateTransform
    {
        private readonly bool generateProject;
        private readonly List<string> modules;
        private readonly string srcSubdir;

        public RustLib(MultiCodeName genNamespace, MultiCodeName commonNs, bool generateProject, string srcSubdir)
        {
            this.generateProject = generateProject;
            this.srcSubdir = srcSubdir;
            this.modules = new List<string> { commonNs.GetFolderName(TargetLanguage.Rust), genNamespace.GetFolderName(TargetLanguage.Rust) };
            this.modules.Sort();
        }

        public string FileName { get => this.generateProject ? "lib.rs" : "mod.rs"; }

        public string FolderPath { get => this.srcSubdir; }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Shared; }
    }
}
