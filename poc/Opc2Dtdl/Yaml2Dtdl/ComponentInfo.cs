namespace Yaml2Dtdl
{
    using System.IO;

    public class ComponentInfo
    {
        public string ModelId { get; set; }

        public string? DisplayName { get; set; }

        public string TypeRef { get; }

        public ComponentInfo(string modelId, string? displayName, string typeRef)
        {
            ModelId = modelId;
            DisplayName = displayName;
            TypeRef = typeRef;
        }

        public void WriteToStream(StreamWriter indexFile, bool addComma)
        {
            indexFile.WriteLine("      {");

            indexFile.WriteLine($"        \"modelId\": \"{ModelId}\",");

            if (DisplayName != null)
            {
                indexFile.WriteLine($"        \"displayName\": \"{DisplayName}\",");
            }

            indexFile.WriteLine($"        \"typeRef\": \"{TypeRef}\"");

            indexFile.WriteLine($"      }}{(addComma ? "," : "")}");
        }
    }
}
