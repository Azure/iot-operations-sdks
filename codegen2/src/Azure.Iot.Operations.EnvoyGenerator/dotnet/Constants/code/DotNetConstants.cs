namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetConstants : IEnvoyTemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName schemaName;
        private readonly CodeName genNamespace;
        private readonly ConstantsSpec constantSpec;
        private readonly bool anyDescriptions;

        public DotNetConstants(string projectName, CodeName schemaName, CodeName genNamespace, ConstantsSpec constantSpec)
        {
            this.projectName = projectName;
            this.schemaName = schemaName;
            this.genNamespace = genNamespace;
            this.constantSpec = constantSpec;
            this.anyDescriptions = constantSpec.Constants.Any(c => c.Value.Description != null);
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Shared; }

        private static string GetDotNetType(string type)
        {
            return type switch
            {
                TDValues.TypeString => "string",
                TDValues.TypeNumber => "double",
                TDValues.TypeInteger => "int",
                TDValues.TypeBoolean => "bool",
                _ => throw new System.ArgumentException($"Unsupported constant type: {type}"),
            };
        }

        private static string GetDotNetValue(object value)
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
