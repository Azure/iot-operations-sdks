namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Xml;

    public record TableColumnSchema
    {
        public TableColumnSchema(XmlElement xmlElt)
        {
            Name = xmlElt.GetAttribute("name");
            Field = xmlElt.GetAttribute("field");
            ConditionOn = xmlElt.GetAttribute("conditionOn");
            TrueValue = xmlElt.HasAttribute("true") ? xmlElt.GetAttribute("true") : "true";
            FalseValue = xmlElt.HasAttribute("false") ? xmlElt.GetAttribute("false") : "false";
        }

        public string Name { get; }

        public string Field { get; }

        public string ConditionOn { get; }

        public string TrueValue { get; }

        public string FalseValue { get; }
    }
}
