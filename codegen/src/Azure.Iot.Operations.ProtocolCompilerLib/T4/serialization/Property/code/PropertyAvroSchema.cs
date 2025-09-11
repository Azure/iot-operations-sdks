namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System.Collections.Generic;
    using DTDLParser;
    using DTDLParser.Models;

    public partial class PropertyAvroSchema : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly ITypeName schema;
        private readonly List<(string, string, DTSchemaInfo, int, bool)> nameDescSchemaIndexFrags;
        private readonly CodeName? sharedPrefix;
        private readonly bool required;
        private readonly HashSet<Dtmi> definedIds;
        private readonly int mqttVersion;

        public PropertyAvroSchema(string projectName, CodeName genNamespace, ITypeName schema, List<(string, string, DTSchemaInfo, int, bool)> nameDescSchemaIndexFrags, CodeName? sharedPrefix, bool required, int mqttVersion)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.nameDescSchemaIndexFrags = nameDescSchemaIndexFrags;
            this.sharedPrefix = sharedPrefix;
            this.required = required;
            this.definedIds = new HashSet<Dtmi>();
            this.mqttVersion = mqttVersion;
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.avsc"; }

        public string FolderPath { get => this.genNamespace.GetFileName(TargetLanguage.Independent); }
    }
}
