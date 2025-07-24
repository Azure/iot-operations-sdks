namespace Azure.Iot.Operations.Services.SchemaRegistry.Models
{
    /// <summary>
    /// Error object for schema operations
    /// </summary>
    public partial class SchemaRegistryError
    {
        /// <summary>
        /// Error code for classification of errors (ex: '400', '404', '500', etc.).
        /// </summary>
        public SchemaRegistryErrorCode Code { get; set; } = default!;

        /// <summary>
        /// Additional details about the error, if available.
        /// </summary>
        public SchemaRegistryErrorDetails? Details { get; set; } = default;

        /// <summary>
        /// Inner error object for nested errors, if applicable.
        /// </summary>
        public SchemaRegistryErrorDetails? InnerError { get; set; } = default;

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string Message { get; set; } = default!;

        /// <summary>
        /// Target of the error, if applicable (e.g., 'schemaType').
        /// </summary>
        public SchemaRegistryErrorTarget? Target { get; set; } = default;
    }
}
