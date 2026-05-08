namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetManagementGroupActionStatus
{
    public ConfigError? Error { get; set; }
    public required string Name { get; set; }
    public MessageSchemaReference? RequestMessageSchemaReference { get; set; }
    public MessageSchemaReference? ResponseMessageSchemaReference { get; set; }

    internal bool EqualTo(AssetManagementGroupActionStatus other)
    {
        if (!string.Equals(Name, other.Name))
        {
            return false;
        }

        if (Error == null && other.Error != null)
        {
            return false;
        }
        else if (Error != null && other.Error == null)
        {
            return false;
        }
        else if (Error != null && other.Error != null)
        {
            if (!Error.EqualTo(other.Error))
            {
                return false;
            }
        }

        if (RequestMessageSchemaReference == null && other.RequestMessageSchemaReference != null)
        {
            return false;
        }
        else if (RequestMessageSchemaReference != null && other.RequestMessageSchemaReference == null)
        {
            return false;
        }
        else if (RequestMessageSchemaReference != null && other.RequestMessageSchemaReference != null)
        {
            if (!RequestMessageSchemaReference.EqualTo(other.RequestMessageSchemaReference))
            {
                return false;
            }
        }

        if (ResponseMessageSchemaReference == null && other.ResponseMessageSchemaReference != null)
        {
            return false;
        }
        else if (ResponseMessageSchemaReference != null && other.ResponseMessageSchemaReference == null)
        {
            return false;
        }
        else if (ResponseMessageSchemaReference != null && other.ResponseMessageSchemaReference != null)
        {
            if (!ResponseMessageSchemaReference.EqualTo(other.ResponseMessageSchemaReference))
            {
                return false;
            }
        }

        return true;
    }
}
