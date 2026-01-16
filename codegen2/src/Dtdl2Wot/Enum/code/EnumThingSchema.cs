namespace Dtdl2Wot
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DTDLParser.Models;

    public partial class EnumThingSchema : ITemplateTransform
    {
        private readonly string enumTitle;
        private readonly DTEnumInfo dtEnum;
        private readonly int indent;
        private readonly string valueSchema;

        public EnumThingSchema(DTEnumInfo dtEnum, int indent)
        {
            this.enumTitle = new CodeName(dtEnum.Id).AsGiven;
            this.dtEnum = dtEnum;
            this.indent = indent;
            this.valueSchema = ThingDescriber.GetPrimitiveType(dtEnum.ValueSchema.Id);
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }

        public static bool CanExpressAsEnum(DTEnumInfo dtEnum)
        {
            switch (ThingDescriber.GetPrimitiveType(dtEnum.ValueSchema.Id))
            {
                case "integer":
                    return dtEnum.EnumValues.All(ev => (int)ev.EnumValue >= 0) // all enum values are non-negative
                        && dtEnum.EnumValues.Min(ev => (int)ev.EnumValue) <= 1 // minimum enum value is 0 or 1
                        && dtEnum.EnumValues.Max(ev => (int)ev.EnumValue) <= dtEnum.EnumValues.Count // maximum enum value does not exceed number of enum values
                        && new HashSet<int>(dtEnum.EnumValues.Select(ev => (int)ev.EnumValue)).Count == dtEnum.EnumValues.Count // all enum values are unique
                        && !dtEnum.EnumValues.Any(ev => ev.Name.Contains(ev.EnumValue.ToString()!)); // no enum value name contains its value
                case "string":
                    return dtEnum.EnumValues.All(ev => ev.Name == (string)ev.EnumValue); // all enum values have matching names and values
                default:
                    throw new InvalidOperationException($"Invalid enum value schema {dtEnum.ValueSchema.Id}");
            }
        }
    }
}
