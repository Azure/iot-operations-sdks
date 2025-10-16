namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetConstants : IEnvoyTemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName schemaName;
        private readonly CodeName genNamespace;
        private readonly List<TypedConstant> constants;
        private readonly bool anyDescriptions;

        public DotNetConstants(string projectName, CodeName schemaName, CodeName genNamespace, List<TypedConstant> constants)
        {
            this.projectName = projectName;
            this.schemaName = schemaName;
            this.genNamespace = genNamespace;
            this.constants = constants;
            this.anyDescriptions = constants.Any(c => c.Description != null);
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }

        private static string GetDotNetType(string type)
        {
            return type switch
            {
                TDValues.TypeString => "string",
                TDValues.TypeNumber => "double",
                TDValues.TypeInteger => "int",
                _ => throw new System.ArgumentException($"Unsupported constant type: {type}"),
            };
        }
    }
}
