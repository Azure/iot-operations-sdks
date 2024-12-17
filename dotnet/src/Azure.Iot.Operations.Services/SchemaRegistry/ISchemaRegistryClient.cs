// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.SchemaRegistry;

using SchemaInfo = dtmi_ms_adr_SchemaRegistry__1.Object_Ms_Adr_SchemaRegistry_Schema__1;
using SchemaFormat = dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_Format__1;
using SchemaType = dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_SchemaType__1;

public interface ISchemaRegistryClient : IAsyncDisposable
{
    /// <summary>
    /// Retrieves schema information from a schema registry service based on the provided schema ID and version.
    /// </summary>
    /// <param name="schemaId">The unique identifier of the schema to retrieve. This is required to locate the schema in the registry.</param>
    /// <param name="version">The version of the schema to fetch. If not specified, defaults to "1.0.0".</param>
    /// <param name="timeout">>An optional timeout for the operation, which specifies the maximum time allowed for the request to complete.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation before completion if needed.</param>
    /// <returns></returns>
    public Task<SchemaInfo?> GetAsync(string schemaId, string version = "1.0.0", TimeSpan? timeout = default!, CancellationToken cancellationToken = default!);
    /// <summary>
    /// Adds or updates a schema in the schema registry service with the specified content, format, type, and metadata.
    /// </summary>
    /// <param name="schemaContent">The content of the schema to be added or updated in the registry.</param>
    /// <param name="schemaFormat">The format of the schema. Specifies how the schema content should be interpreted.</param>
    /// <param name="schemaType">The type of the schema, such as message schema or data schema. Defaults to <see cref="SchemaType.MessageSchema"/>.</param>
    /// <param name="version">The version of the schema to add or update. If not specified, defaults to "1.0.0".</param>
    /// <param name="tags">Optional metadata tags to associate with the schema. These tags can be used to store additional information 
    /// about the schema in key-value format.</param>
    /// <param name="timeout">>An optional timeout for the operation, which specifies the maximum time allowed for the request to complete.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation before completion if needed.</param>
    /// <returns></returns>
    public Task<SchemaInfo?> PutAsync(string schemaContent, SchemaFormat schemaFormat, SchemaType schemaType = SchemaType.MessageSchema, string version = "1.0.0", Dictionary<string, string> tags = default!, TimeSpan? timeout = default!, CancellationToken cancellationToken = default!);
}
