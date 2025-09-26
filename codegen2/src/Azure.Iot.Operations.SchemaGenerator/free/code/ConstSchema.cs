namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    public partial class ConstSchema : ISchemaTemplateTransform
    {
        string name;
        List<(string, int)> constValues;
        string genNamespace;

        internal ConstSchema(string name, Dictionary<string, TDDataSchema> schemaDefinitions, string genNamespace)
        {
            this.name = name;
            this.constValues = schemaDefinitions.Where(d => d.Value.Type == TDValues.TypeInteger && d.Value.Const != null).Select(d => (d.Key, (int)d.Value.Const!)).OrderBy(d => d.Item2).ToList();
            this.genNamespace = genNamespace;
        }

        public string FileName { get => $"{this.name}.const.json"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
