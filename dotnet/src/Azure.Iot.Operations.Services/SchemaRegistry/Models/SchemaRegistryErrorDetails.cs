namespace Azure.Iot.Operations.Services.SchemaRegistry.Models
{
    /// <summary>
    /// Additional details about an error
    /// </summary>
    public partial class SchemaRegistryErrorDetails
    {
        /// <summary>
        /// Multi-part error code for classification and root causing of errors (e.g., '400.200').
        /// </summary>
        public string? Code { get; set; } = default;

        /// <summary>
        /// Correlation ID for tracing the error across systems.
        /// </summary>
        public string? CorrelationId { get; set; } = default;

        /// <summary>
        /// Human-readable helpful error message to provide additional context for the error
        /// </summary>
        public string? Message { get; set; } = default;

    }
}
