using System.Text.Json.Serialization;

public class DatasetWriteRequest
{
    [JsonConstructor]
    public DatasetWriteRequest(IReadOnlyDictionary<string, DataValue> dataValues)
    {
        DataValues = dataValues;
    }

    public IReadOnlyDictionary<string, DataValue> DataValues { get; }
}
