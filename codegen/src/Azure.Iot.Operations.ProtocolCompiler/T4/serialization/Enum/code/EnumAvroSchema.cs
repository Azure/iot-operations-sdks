namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class EnumAvroSchema : ITemplateTransform
    {
        private readonly string? schema;
        private readonly List<string> names;
        private readonly int indent;

        public EnumAvroSchema(string? schema, List<string> names, int indent)
        {
            this.schema = schema;
            this.names = names;
            this.indent = indent;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
