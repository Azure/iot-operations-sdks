namespace Akri.Dtdl.Codegen
{
    public partial class RustSerialization : ITemplateTransform
    {
        private readonly string serializerSubNamespace;
        private readonly bool isSchematized;

        public RustSerialization(string serializerSubNamespace, bool isSchematized)
        {
            this.serializerSubNamespace = serializerSubNamespace.ToLowerInvariant();
            this.isSchematized = isSchematized;
        }

        public string FileName { get => "serialization.rs"; }

        public string FolderPath { get => SubPaths.Rust; }
    }
}
