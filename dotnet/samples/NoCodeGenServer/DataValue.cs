using System.Text.Json.Serialization;

public class DataValue
{
    [JsonConstructor]
    public DataValue(
        object? value)
    {
        Value = value;
    }

    public object? Value { get; set; }
}
