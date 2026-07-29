
namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public partial class RustPropertyMaintainer : ITemplateTransform
    {
        private readonly CodeName? propertyName;
        private readonly CodeName propSchema;
        private readonly CodeName genNamespace;
        private readonly EmptyTypeName serializerEmptyType;
        private readonly CodeName readRespSchema;
        private readonly CodeName? readRespNamespace;
        private readonly CodeName? writeReqSchema;
        private readonly CodeName? writeReqNamespace;
        private readonly CodeName? writeRespSchema;
        private readonly CodeName? writeRespNamespace;
        private readonly CodeName? propValueName;
        private readonly CodeName? readErrorName;
        private readonly CodeName? readErrorSchema;
        private readonly CodeName? readErrorNamespace;
        private readonly CodeName? writeErrorName;
        private readonly CodeName? writeErrorSchema;
        private readonly CodeName? writeErrorNamespace;
        private readonly bool doesPropertyTargetMaintainer;
        private readonly bool separateProperties;
        private readonly CodeName containingComponentName;
        private readonly string readCommandName;
        private readonly string writeCommandName;

        public RustPropertyMaintainer(
            CodeName? propertyName,
            CodeName propSchema,
            CodeName genNamespace,
            EmptyTypeName serializerEmptyType,
            CodeName readRespSchema,
            CodeName? readRespNamespace,
            CodeName? writeReqSchema,
            CodeName? writeReqNamespace,
            CodeName? writeRespSchema,
            CodeName? writeRespNamespace,
            CodeName? propValueName,
            CodeName? readErrorName,
            CodeName? readErrorSchema,
            CodeName? readErrorNamespace,
            CodeName? writeErrorName,
            CodeName? writeErrorSchema,
            CodeName? writeErrorNamespace,
            bool doesPropertyTargetMaintainer,
            bool separateProperties)
        {
            this.propertyName = propertyName ?? new CodeName(string.Empty);
            this.propSchema = propSchema;
            this.genNamespace = genNamespace;
            this.serializerEmptyType = serializerEmptyType;
            this.readRespSchema = readRespSchema;
            this.readRespNamespace = readRespNamespace;
            this.writeReqSchema = writeReqSchema;
            this.writeReqNamespace = writeReqNamespace;
            this.writeRespSchema = writeRespSchema;
            this.writeRespNamespace = writeRespNamespace;
            this.propValueName = propValueName;
            this.readErrorName = readErrorName;
            this.readErrorSchema = readErrorSchema;
            this.readErrorNamespace = readErrorNamespace;
            this.writeErrorName = writeErrorName;
            this.writeErrorSchema = writeErrorSchema;
            this.writeErrorNamespace = writeErrorNamespace;
            this.doesPropertyTargetMaintainer = doesPropertyTargetMaintainer;
            this.separateProperties = separateProperties;
            this.containingComponentName = new CodeName(this.propertyName ?? new CodeName(string.Empty), "property", "maintainer");
            this.readCommandName = this.propertyName != null ? $"Read{this.propertyName.AsGiven}" : $"Read{SchemaNames.AggregatePropSchema.AsGiven}";
            this.writeCommandName = this.propertyName != null ? $"Write{this.propertyName.AsGiven}" : $"Write{SchemaNames.AggregatePropSchema.AsGiven}";
        }

        public string FileName { get => $"{this.containingComponentName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Rust); }
    }
}
