// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustConstants : IEnvoyTemplateTransform
    {
        private readonly CodeName schemaName;
        private readonly MultiCodeName genNamespace;
        private readonly MultiCodeName commonNs;
        private readonly ConstantsSpec constantSpec;
        private readonly string srcSubdir;

        public RustConstants(CodeName schemaName, MultiCodeName genNamespace, MultiCodeName commonNs, ConstantsSpec constantSpec, string srcSubdir)
        {
            this.schemaName = schemaName;
            this.genNamespace = genNamespace;
            this.commonNs = commonNs;
            this.constantSpec = constantSpec;
            this.srcSubdir = srcSubdir;
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Shared; }

        private static string GetRustType(string type)
        {
            return type switch
            {
                TDValues.TypeString => "&str",
                TDValues.TypeNumber => "f64",
                TDValues.TypeInteger => "i32",
                TDValues.TypeBoolean => "bool",
                _ => throw new System.ArgumentException($"Unsupported constant type: {type}"),
            };
        }

        private static string GetRustValue(object value)
        {
            return value switch
            {
                string s => $"\"{s}\"",
                double d => d.ToString(CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                _ => throw new System.ArgumentException($"Unsupported constant value type: {value.GetType()}"),
            };
        }
    }
}
