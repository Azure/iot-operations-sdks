// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustIndex : IEnvoyTemplateTransform
    {
        private readonly MultiCodeName genNamespace;
        private readonly MultiCodeName commonNs;
        private readonly List<string>? clientModules;
        private readonly List<string>? serverModules;
        private readonly List<string> allModules;
        private readonly string srcSubdir;

        public RustIndex(
            MultiCodeName genNamespace,
            MultiCodeName commonNs,
            List<string> clientFilenames,
            List<string> serverFilenames,
            List<string> sharedFilenames,
            List<string> hiddenFilenames,
            string srcSubdir)
        {
            this.genNamespace = genNamespace;
            this.commonNs = commonNs;
            this.clientModules = clientFilenames.Count > 0 ? clientFilenames.Concat(sharedFilenames).Select(f => Path.GetFileNameWithoutExtension(f)).Order().ToList() : null;
            this.serverModules = serverFilenames.Count > 0 ? serverFilenames.Concat(sharedFilenames).Select(f => Path.GetFileNameWithoutExtension(f)).Order().ToList() : null;
            this.allModules = sharedFilenames.Concat(hiddenFilenames).Concat(clientFilenames).Concat(serverFilenames).Select(f => Path.GetFileNameWithoutExtension(f)).Order().ToList();
            this.srcSubdir = srcSubdir;
        }

        public string FileName { get => $"{this.genNamespace.GetFolderName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.srcSubdir; }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Shared; }
    }
}
