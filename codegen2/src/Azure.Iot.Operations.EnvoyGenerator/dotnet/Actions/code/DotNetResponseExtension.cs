namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetResponseExtension : IEnvoyTemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly ITypeName respSchema;
        private readonly CodeName headerCodeName;
        private readonly CodeName headerCodeSchema;
        private readonly CodeName? headerInfoName;
        private readonly CodeName? headerInfoSchema;
        private readonly List<string> headerCodeValues;
        private readonly bool generateClient;
        private readonly bool generateServer;

        public DotNetResponseExtension(
            string projectName,
            CodeName genNamespace,
            ITypeName respSchema,
            CodeName headerCodeName,
            CodeName headerCodeSchema,
            CodeName? headerInfoName,
            CodeName? headerInfoSchema,
            List<string> headerCodeValues,
            bool generateClient,
            bool generateServer)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.respSchema = respSchema;
            this.headerCodeName = headerCodeName;
            this.headerCodeSchema = headerCodeSchema;
            this.headerInfoName = headerInfoName;
            this.headerInfoSchema = headerInfoSchema;
            this.headerCodeValues = headerCodeValues;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
        }

        public string FileName { get => $"{this.respSchema.GetFileName(TargetLanguage.CSharp, "extensions")}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Shared; }
    }
}
