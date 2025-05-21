namespace SemanticDataHub
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json.Linq;
    using Common;

    internal class ConversionTransformer : IDataTransformer
    {
        private static readonly Regex quantRegex = new Regex(@"^(\w+)\((\w+)\)$", RegexOptions.Compiled);

        private static JObject quantitativeTypeMapObj;
        private static JObject? modelUnitsObj;

        private SelectionTransformer selectionTransformer;

        private double scale;
        private double offset;

        static ConversionTransformer()
        {
            using (StreamReader reader = File.OpenText(Constants.QuantitativeTypeMapFilePath))
            {
                quantitativeTypeMapObj = JObject.Parse(reader.ReadToEnd());
            }
        }

        public static void ConfigureModelUnits(string modelUnitsFilePath)
        {
            using (StreamReader reader = File.OpenText(modelUnitsFilePath))
            {
                modelUnitsObj = JObject.Parse(reader.ReadToEnd());
            }
        }

        public ConversionTransformer(string propertyPath, JsonElement elt1, JsonElement elt2, string bindingFileName)
        {
            selectionTransformer = new SelectionTransformer(elt1, bindingFileName);

            if (modelUnitsObj == null)
            {
                throw new Exception("Model units not configured");
            }

            GetQuantitativeTypeAndUnit(elt2.GetString()!, out string fromQuantitativeType, out string fromQuantitativeUnit);
            GetQuantitativeTypeAndUnit(((JValue)modelUnitsObj.GetValue(propertyPath)!).Value<string>()!, out string toQuantitativeType, out string toQuantitativeUnit);
            if (fromQuantitativeType != toQuantitativeType)
            {
                throw new Exception($"Inconsistent quantitative types for property path {propertyPath}: model specifies {toQuantitativeType} but binding specifies {fromQuantitativeType}");
            }

            if (fromQuantitativeUnit == toQuantitativeUnit)
            {
                scale = 1.0;
                offset = 0.0;
                return;
            }

            JToken? conversionToken = quantitativeTypeMapObj.SelectToken($"$.{fromQuantitativeType}[?(@.from=='{fromQuantitativeUnit}'&&@.to=='{toQuantitativeUnit}')]");
            if (conversionToken != null)
            {
                scale = ((JValue)((JObject)conversionToken).GetValue("scale")!).Value<double>();
                offset = ((JValue)((JObject)conversionToken).GetValue("offset")!).Value<double>();
            }
        }

        public JToken? TransformData(JToken data)
        {
            JToken? selectedToken = selectionTransformer.TransformData(data);
            if (selectedToken == null || selectedToken.Type != JTokenType.Float && selectedToken.Type != JTokenType.Integer)
            {
                return null;
            }

            return new JValue(((JValue)selectedToken).Value<double>() * scale + offset);
        }

        private static void GetQuantitativeTypeAndUnit(string quantExpr, out string quantType, out string quantUnit)
        {
            Match quantMatch = quantRegex.Match(quantExpr);
            if (!quantMatch.Success)
            {
                throw new Exception($"failed to parse quantitative type expression {quantExpr}");
            }

            quantType = quantMatch.Groups[1].Captures[0].Value;
            quantUnit = quantMatch.Groups[2].Captures[0].Value;
        }
    }
}
