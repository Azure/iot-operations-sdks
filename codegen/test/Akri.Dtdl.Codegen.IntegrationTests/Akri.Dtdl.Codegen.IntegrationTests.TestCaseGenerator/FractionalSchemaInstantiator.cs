namespace Akri.Dtdl.Codegen.IntegrationTests.TestCaseGenerator
{
    using System;
    using System.Linq;
    using System.Text.Json;

    public class FractionalSchemaInstantiator : IPrimitiveSchemaInstantiator
    {
        private readonly Func<double> getNextValue;

        public FractionalSchemaInstantiator(JsonElement configElt)
        {
            if (configElt.ValueKind == JsonValueKind.Array)
            {
                double[] values = configElt.EnumerateArray().Select(e => e.GetDouble()!).ToArray();
                int valueIx = 0;
                getNextValue = () =>
                {
                    double value = values[valueIx];
                    valueIx = (valueIx + 1) % values.Length;
                    return value;
                };
            }
            else if (configElt.ValueKind == JsonValueKind.Object)
            {
                double initial = configElt.GetProperty("initial").GetDouble();
                bool carrySign = configElt.GetProperty("carrySign").GetBoolean();
                double multiplier = configElt.GetProperty("multiplier").GetDouble();
                double modulus = configElt.GetProperty("modulus").GetDouble();
                double addend = configElt.GetProperty("addend").GetDouble();
                int decimals = configElt.GetProperty("decimals").GetInt32();

                double currentValue = initial;
                getNextValue = () =>
                {
                    double value = currentValue;
                    currentValue = Math.Round((carrySign ? currentValue : Math.Abs(currentValue)) * multiplier % modulus + addend, decimals);
                    return value;
                };
            }
            else
            {
                throw new Exception($"FractionalSchemaInstantiator constructor does not accept JSON element of type {configElt.ValueKind}");
            }
        }

        public void InstantiateSchema(Utf8JsonWriter jsonWriter)
        {
            double value = getNextValue();
            jsonWriter.WriteNumberValue(value);
        }
    }
}
