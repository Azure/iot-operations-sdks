namespace Opc2Yaml
{
    using System.Xml;

    public class ManagedXmlDocument
    {
        private XmlDocument xmlDocument;

        public ManagedXmlDocument(string filePath)
        {
            xmlDocument = new XmlDocument();
            xmlDocument.Load(filePath);

            NamespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            NamespaceManager.AddNamespace("opc", "http://opcfoundation.org/UA/2011/03/UANodeSet.xsd");
            NamespaceManager.AddNamespace("ua", "http://unifiedautomation.com/Configuration/NodeSet.xsd");
            NamespaceManager.AddNamespace("uax", "http://opcfoundation.org/UA/2008/02/Types.xsd");
        }

        public XmlElement RootElement { get => xmlDocument.DocumentElement!; }

        public XmlNamespaceManager NamespaceManager { get; }
    }
}
