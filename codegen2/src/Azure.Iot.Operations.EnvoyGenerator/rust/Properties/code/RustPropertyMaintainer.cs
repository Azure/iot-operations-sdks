namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustPropertyMaintainer : IEnvoyTemplateTransform
    {
        private readonly CodeName? propertyName;
        private readonly CodeName propSchema;
        private readonly CodeName componentName;
        private readonly string readCommandName;
        private readonly string writeCommandName;
        private readonly CodeName genNamespace;
        private readonly EmptyTypeName readSerializerEmptyType;
        private readonly EmptyTypeName writeSerializerEmptyType;
        private readonly CodeName? readRespSchema;
        private readonly CodeName? writeReqSchema;
        private readonly CodeName? writeRespSchema;
        private readonly CodeName? propValueName;
        private readonly CodeName? readErrorName;
        private readonly CodeName? readErrorSchema;
        private readonly CodeName? writeErrorName;
        private readonly CodeName? writeErrorSchema;
        private readonly string readTopicPattern;
        private readonly string writeTopicPattern;
        private readonly string srcSubdir;
        private readonly bool separateProperties;

        public RustPropertyMaintainer(
            string propertyName,
            CodeName propSchema,
            string componentName,
            string readCommandName,
            string writeCommandName,
            CodeName genNamespace,
            EmptyTypeName readSerializerEmptyType,
            EmptyTypeName writeSerializerEmptyType,
            string? readRespSchema,
            string? writeReqSchema,
            string? writeRespSchema,
            string? propValueName,
            string? readErrorName,
            string? readErrorSchema,
            string? writeErrorName,
            string? writeErrorSchema,
            string readTopicPattern,
            string writeTopicPattern,
            string srcSubdir,
            bool separateProperties)
        {
            this.propertyName = new CodeName(propertyName);
            this.propSchema = propSchema;
            this.componentName = new CodeName(componentName);
            this.readCommandName = readCommandName;
            this.writeCommandName = writeCommandName;
            this.genNamespace = genNamespace;
            this.readSerializerEmptyType = readSerializerEmptyType;
            this.writeSerializerEmptyType = writeSerializerEmptyType;
            this.readRespSchema = readRespSchema != null ? new CodeName(readRespSchema) : null;
            this.writeReqSchema = writeReqSchema != null ? new CodeName(writeReqSchema) : null;
            this.writeRespSchema = writeRespSchema != null ? new CodeName(writeRespSchema) : null;
            this.propValueName = propValueName != null ? new CodeName(propValueName) : null;
            this.readErrorName = readErrorName != null ? new CodeName(readErrorName) : null;
            this.readErrorSchema = readErrorSchema != null ? new CodeName(readErrorSchema) : null;
            this.writeErrorName = writeErrorName != null ? new CodeName(writeErrorName) : null;
            this.writeErrorSchema = writeErrorSchema != null ? new CodeName(writeErrorSchema) : null;
            this.readTopicPattern = readTopicPattern;
            this.writeTopicPattern = writeTopicPattern;
            this.srcSubdir = srcSubdir;
            this.separateProperties = separateProperties;
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
