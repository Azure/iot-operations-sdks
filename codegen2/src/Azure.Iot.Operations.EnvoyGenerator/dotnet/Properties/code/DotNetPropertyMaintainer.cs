namespace Azure.Iot.Operations.EnvoyGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetPropertyMaintainer : IEnvoyTemplateTransform
    {
        private readonly CodeName propertyName;
        private readonly CodeName componentName;
        private readonly CodeName readerName;
        private readonly CodeName writerName;
        private readonly string readCommandName;
        private readonly string writeCommandName;
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName serviceName;
        private readonly string readSerializerClassName;
        private readonly EmptyTypeName readSerializerEmptyType;
        private readonly string writeSerializerClassName;
        private readonly EmptyTypeName writeSerializerEmptyType;
        private readonly CodeName? readRespSchema;
        private readonly CodeName? writeReqSchema;
        private readonly CodeName? writeRespSchema;
        private readonly string readTopicPattern;
        private readonly string writeTopicPattern;

        public DotNetPropertyMaintainer(
            string propertyName,
            string componentName,
            string readerName,
            string writerName,
            string readCommandName,
            string writeCommandName,
            string projectName,
            CodeName genNamespace,
            CodeName serviceName,
            string readSerializerClassName,
            EmptyTypeName readSerializerEmptyType,
            string writeSerializerClassName,
            EmptyTypeName writeSerializerEmptyType,
            string? readRespSchema,
            string? writeReqSchema,
            string? writeRespSchema,
            string readTopicPattern,
            string writeTopicPattern)
        {
            this.propertyName = new CodeName(propertyName);
            this.componentName = new CodeName(componentName);
            this.readerName = new CodeName(readerName);
            this.writerName = new CodeName(writerName);
            this.readCommandName = readCommandName;
            this.writeCommandName = writeCommandName;
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.serviceName = serviceName;
            this.readSerializerClassName = readSerializerClassName;
            this.readSerializerEmptyType = readSerializerEmptyType;
            this.writeSerializerClassName = writeSerializerClassName;
            this.writeSerializerEmptyType = writeSerializerEmptyType;
            this.readRespSchema = readRespSchema != null ? new CodeName(readRespSchema) : null;
            this.writeReqSchema = writeReqSchema != null ? new CodeName(writeReqSchema) : null;
            this.writeRespSchema = writeRespSchema != null ? new CodeName(writeRespSchema) : null;
            this.readTopicPattern = readTopicPattern;
            this.writeTopicPattern = writeTopicPattern;
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
