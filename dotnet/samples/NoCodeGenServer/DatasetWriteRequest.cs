using Opc.Ua;

public class DatasetWriteRequest
{
    public DatasetWriteRequest(IReadOnlyDictionary<string, DataValue> dataValues)
    {
        DataValues = dataValues;
    }

    public IReadOnlyDictionary<string, DataValue> DataValues { get; }
}
