using Json.Schema;

namespace Azure.Iot.Operations.Services.SchemaRegistry.Host;

public class SchemaValidator(ILogger<SchemaValidator> logger)
{
    public bool ValidateSchema(string? schemaContent, string schemaFormat) 
        => schemaFormat switch
    {
        var format when format.Contains("Json") => ValidateJson(schemaContent!),
        _ => false,
    };

    private bool ValidateJson(string schemaContent)
    {
        try
        {
            JsonSchema schema = JsonSchema.FromText(schemaContent);
            if (schema.TryGetKeyword<SchemaKeyword>(out var keyword))
            {
                if (keyword.Schema.ToString() == "https://json-schema.org/draft-07/schema#")
                {
                    return true;
                }
                else
                {
                    logger.LogError("Json schema validation failed: Invalid schema version");
                    return false;
                }
            }
            else
            {
                logger.LogError("Json schema validation failed: Invalid schema version");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Json schema validation failed: {ex}", ex.Message);
            return false;
        }
    }
}
