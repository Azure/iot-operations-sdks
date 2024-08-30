namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class CompletenessChecker
    {
        private static HashSet<string> undocumentedItems = new();

        private HashSet<string> schemaNames;

        public static void ProcessHeader(XmlElement headerElt)
        {
            for (int i = 0; i < headerElt.ChildNodes.Count; i++)
            {
                XmlNode xmlNode = headerElt.ChildNodes[i]!;
                if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name == "Undocumented")
                {
                    XmlElement xmlElement = (XmlElement)xmlNode;
                    undocumentedItems.Add(xmlElement.GetAttribute("item"));
                }
            }
        }

        public CompletenessChecker(IEnumerable<string> names)
        {
            this.schemaNames = new HashSet<string>(names);
        }

        public void Tally(string itemName)
        {
            this.schemaNames.Remove(itemName);
        }

        public void CheckCompleteness()
        {
            if (this.schemaNames.Any(n => !undocumentedItems.Contains(n)))
            {
                Alert.Warning($"proto-doc lacks documentation for schema(s): {string.Join(", ", this.schemaNames.Where(n => !undocumentedItems.Contains(n)))}");
            }
        }
    }
}
