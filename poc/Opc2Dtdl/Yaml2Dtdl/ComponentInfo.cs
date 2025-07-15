namespace Yaml2Dtdl
{
    using System.IO;

    public class ComponentInfo
    {
        public string ModelId { get; set; }

        public string SpecVer { get; set; }

        public string? DisplayName { get; set; }

        public string TypeRef { get; }

        public ComponentInfo(string modelId, string specVer, string? displayName, string typeRef)
        {
            ModelId = modelId;
            SpecVer = specVer;
            DisplayName = displayName;
            TypeRef = typeRef;
        }

        public void WriteToStream(StreamWriter indexFile, bool addComma)
        {
            indexFile.WriteLine("      {");

            indexFile.WriteLine($"        \"modelId\": \"{ModelId}\",");
            indexFile.WriteLine($"        \"specVersion\": \"{SpecVer}\",");

            if (DisplayName != null)
            {
                indexFile.WriteLine($"        \"displayName\": \"{DisplayName}\",");
            }

            indexFile.WriteLine($"        \"typeRef\": \"{TypeRef}\"");

            indexFile.WriteLine($"      }}{(addComma ? "," : "")}");
        }
    }
}
