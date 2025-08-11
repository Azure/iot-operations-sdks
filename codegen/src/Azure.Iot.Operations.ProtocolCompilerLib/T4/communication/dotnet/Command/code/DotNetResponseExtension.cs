
namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public partial class DotNetResponseExtension : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly ITypeName respSchema;
        private readonly CodeName errorCodeName;
        private readonly CodeName errorCodeSchema;
        private readonly CodeName? errorInfoName;
        private readonly CodeName? errorInfoSchema;
        Dictionary<CodeName, string> errorCodeEnumeration;
        private readonly CodeName? addlNamespace;
        private readonly bool generateClient;
        private readonly bool generateServer;

        public DotNetResponseExtension(
            string projectName,
            CodeName genNamespace,
            ITypeName respSchema,
            CodeName? respNamespace,
            CodeName errorCodeName,
            CodeName errorCodeSchema,
            CodeName? errorCodeNamespace,
            CodeName? errorInfoName,
            CodeName? errorInfoSchema,
            CodeName? errorInfoNamespace,
            Dictionary<CodeName, string> errorCodeEnumeration,
            bool generateClient,
            bool generateServer)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.respSchema = respSchema;
            this.errorCodeName = errorCodeName;
            this.errorCodeSchema = errorCodeSchema;
            this.errorInfoName = errorInfoName;
            this.errorInfoSchema = errorInfoSchema;
            this.errorCodeEnumeration = errorCodeEnumeration;
            this.addlNamespace = respNamespace ?? errorCodeNamespace ?? errorInfoNamespace;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
        }

        public string FileName { get => $"{this.respSchema.GetFileName(TargetLanguage.CSharp, "extensions")}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
