namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class ArrayProto3 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string schema;
        private readonly DTSchemaInfo elementSchema;
        private readonly DtmiToSchemaName dtmiToSchemaName;
        private readonly HashSet<string> importNames;

        public ArrayProto3(string projectName, string genNamespace, string schema, DTSchemaInfo elementSchema, DtmiToSchemaName dtmiToSchemaName)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.elementSchema = elementSchema;
            this.dtmiToSchemaName = dtmiToSchemaName;
            this.importNames = new HashSet<string>();
        }

        public string FileName { get => $"{this.schema}.proto"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
