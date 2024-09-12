namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.TestCaseGenerator
{
    using System;
    using System.Linq;
    using System.Text.Json;

    public class IntegralSchemaInstantiator : IPrimitiveSchemaInstantiator
    {
        private readonly Func<long> getNextValue;

        public IntegralSchemaInstantiator(JsonElement configElt)
        {
            if (configElt.ValueKind == JsonValueKind.Array)
            {
                long[] values = configElt.EnumerateArray().Select(e => e.GetInt64()!).ToArray();
                int valueIx = 0;
                getNextValue = () =>
                {
                    long value = values[valueIx];
                    valueIx = (valueIx + 1) % values.Length;
                    return value;
                };
            }
            else if (configElt.ValueKind == JsonValueKind.Object)
            {
                long initial = configElt.GetProperty("initial").GetInt64();
                bool carrySign = configElt.GetProperty("carrySign").GetBoolean();
                long multiplier = configElt.GetProperty("multiplier").GetInt64();
                long modulus = configElt.GetProperty("modulus").GetInt64();
                long addend = configElt.GetProperty("addend").GetInt64();

                long currentValue = initial;
                getNextValue = () =>
                {
                    long value = currentValue;
                    currentValue = (carrySign ? currentValue : Math.Abs(currentValue)) * multiplier % modulus + addend;
                    return value;
                };
            }
            else
            {
                throw new Exception($"IntegralSchemaInstantiator constructor does not accept JSON element of type {configElt.ValueKind}");
            }
        }

        public void InstantiateSchema(Utf8JsonWriter jsonWriter)
        {
            long value = getNextValue();
            jsonWriter.WriteNumberValue(value);
        }
    }
}
