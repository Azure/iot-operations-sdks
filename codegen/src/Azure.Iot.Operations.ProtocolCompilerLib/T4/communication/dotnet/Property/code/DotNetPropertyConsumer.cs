
namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public partial class DotNetPropertyConsumer : ITemplateTransform
    {
        private readonly CodeName? propertyName;
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly string modelId;
        private readonly CodeName serviceName;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly EmptyTypeName serializerEmptyType;
        private readonly CodeName readRespSchema;
        private readonly CodeName? readRespNamespace;
        private readonly CodeName? writeReqSchema;
        private readonly CodeName? writeReqNamespace;
        private readonly CodeName? writeRespSchema;
        private readonly CodeName? writeRespNamespace;
        private readonly CodeName containingComponentName;
        private readonly string readCommandName;
        private readonly string writeCommandName;

        public DotNetPropertyConsumer(
            CodeName? propertyName,
            string projectName,
            CodeName genNamespace,
            string modelId,
            CodeName serviceName,
            string serializerSubNamespace,
            string serializerClassName,
            EmptyTypeName serializerEmptyType,
            CodeName readRespSchema,
            CodeName? readRespNamespace,
            CodeName? writeReqSchema,
            CodeName? writeReqNamespace,
            CodeName? writeRespSchema,
            CodeName? writeRespNamespace)
        {
            this.propertyName = propertyName;
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = serializerClassName;
            this.serializerEmptyType = serializerEmptyType;
            this.readRespSchema = readRespSchema;
            this.readRespNamespace = readRespNamespace;
            this.writeReqSchema = writeReqSchema;
            this.writeReqNamespace = writeReqNamespace;
            this.writeRespSchema = writeRespSchema;
            this.writeRespNamespace = writeRespNamespace;
            this.containingComponentName = new CodeName(this.propertyName ?? new CodeName(string.Empty), "property", "consumer");
            this.readCommandName = this.propertyName != null ? $"Read{this.propertyName.AsGiven}" : $"Read{SchemaNames.AggregatePropSchema.AsGiven}";
            this.writeCommandName = this.propertyName != null ? $"Write{this.propertyName.AsGiven}" : $"Write{SchemaNames.AggregatePropSchema.AsGiven}";
        }

        public string FileName { get => $"{this.containingComponentName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
