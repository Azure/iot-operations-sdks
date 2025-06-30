namespace Yaml2Dtdl
{
    using System.IO;
    using System.Collections.Generic;

    public class SpecInfo
    {
        public string FileName { get; set; }

        public string Ontology { get; set; }

        public List<ComponentInfo> Events { get; }

        public List<ComponentInfo> Composites { get; }

        public List<ComponentInfo> OtherTypes { get; }

        public SpecInfo(string fileName, string ontology)
        {
            FileName = fileName;
            Ontology = ontology;
            Events = new();
            Composites = new();
            OtherTypes = new();
        }

        public void AddComponent(string modelId, string? displayName, string typeRef, bool isComposite, bool isEvent)
        {
            ComponentInfo component = new ComponentInfo(modelId, displayName, typeRef);

            if (isEvent)
            {
                Events.Add(component);
            }
            else if (isComposite)
            {
                Composites.Add(component);
            }
            else
            {
                OtherTypes.Add(component);
            }
        }

        public void WriteToStream(StreamWriter indexFile, bool addComma)
        {
            indexFile.WriteLine("  {");

            indexFile.WriteLine($"    \"file\": \"{FileName}\",");
            indexFile.WriteLine($"    \"ontology\": \"{Ontology}\",");

            indexFile.WriteLine("    \"events\": [");
            WriteComponentsToStream(indexFile, Events);
            indexFile.WriteLine("    ],");

            indexFile.WriteLine("    \"composites\": [");
            WriteComponentsToStream(indexFile, Composites);
            indexFile.WriteLine("    ],");

            indexFile.WriteLine("    \"otherTypes\": [");
            WriteComponentsToStream(indexFile, OtherTypes);
            indexFile.WriteLine("    ]");

            indexFile.WriteLine($"  }}{(addComma ? "," : "")}");
        }

        private static void WriteComponentsToStream(StreamWriter indexFile, List<ComponentInfo> components)
        {
            int ix = 1;
            foreach (ComponentInfo component in components)
            {
                component.WriteToStream(indexFile, addComma: ix < components.Count);
                ix++;
            }
        }
    }
}
