// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Services.EdgeRegistry;
using Azure.Iot.Operations.Services.EdgeRegistry.Generated;
using Azure.Iot.Operations.Services.EdgeRegistry.Models;

string jsonSchema = /*lang=json,strict*/ """
    {
        "$schema": "http://json-schema.org/draft-07/schema#",
        "type": "object",
        "properties": {
            "humidity": {
                "type": "integer"
            },
            "temperature": {
                "type": "number"
            },
            "pressure": {
                "type": "integer"
            }
        }
    }
    """;

string thingDescription = /*lang=json,strict*/ """
    {
        "@context": [
            "https://www.w3.org/2022/wot/td/v1.1",
            {
            "dov": "http://azure.com/IoT/operations/tm#",
            "ov": "http://azure.com/IoT/operations/ontology#",
            "adr": "http://azure.com/IoT/operations/deviceregistry#"
            }
        ],

        "id": "urn:uuid:00000000-1111-2222-3333-444444444444",
        "title": "sampleThingDescription",

        "securityDefinitions": {
            "nosec_sc": {
            "scheme": "nosec"
            }
        },
        "security": ["nosec_sc"],

        "links": [
            {
            "rel": "adr:asset",
            "href": "urn:uuid:00000000-1111-2222-3333-444444444444"
            },
            {
            "rel": "dov:dataset",
            "href": "#ALERT",
            "title": "ALERT"
            }
        ],

        "properties": {
            "ALERT/ALERT": {
            "dov:memberOf": "ALERT",
            "@type": ["ov:measurement"],
            "dov:dataSource": "ALERT",
            "dov:propertyIRI": "#ALERT/ALERT",
            "title": "ALERT",
            "forms": []
            }
        }
    }
    """;

MqttSessionClient mqttClient = new();
await mqttClient.ConnectAsync(MqttConnectionSettings.FromEnvVars());
ApplicationContext applicationContext = new();

await using EdgeRegistryClient edgeRegistryClient = new(applicationContext, mqttClient, "azure_iot_operations");

try
{
    // -----------------------------------------------------------------------
    // Schema Extension
    // -----------------------------------------------------------------------
    string schemaId = "exampleSchema2";

    // Create schema group
    try
    {
        Group schemaGroup = await edgeRegistryClient.CreateSchemaGroupAsync(
            new GroupCreateAttributes { Name = "SDK Schemas" });
        Console.WriteLine($"Created schema group: {schemaGroup.Id}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Create schema group error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Get schema group
    try
    {
        Group schemaGroup = await edgeRegistryClient.GetSchemaGroupAsync();
        Console.WriteLine($"Got schema group: {schemaGroup.Id}, name: {schemaGroup.Name}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Get schema group error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Create schema with initial version
    ulong? versionId1 = null;
    try
    {
        Schema schema = await edgeRegistryClient.CreateSchemaAsync(
            schemaId,
            new CreateSchemaVersionOptions
            {
                SchemaDocument = System.Text.Encoding.UTF8.GetBytes(jsonSchema),
                ContentType = "application/json",
                Name = "Example Schema Version 1",
                Description = "An example schema version",
                Format = SchemaFormat.JsonSchemaDraft07,
            });
        Console.WriteLine($"Created schema: {schema.Resource.Id}, default version: {schema.MetaExtensions.DefaultVersionId}");
        versionId1 = schema.MetaExtensions.DefaultVersionId;
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Create schema error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Get schema
    try
    {
        Schema schema = await edgeRegistryClient.GetSchemaAsync(schemaId);
        Console.WriteLine($"Got schema: {schema.Resource.Id}, versions count: {schema.Resource.VersionsCount}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Get schema error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Get schema version 1
    if (versionId1 is not null)
    {
        try
        {
            SchemaVersion schemaVersion = await edgeRegistryClient.GetSchemaVersionAsync(schemaId, versionId1.Value);
            Console.WriteLine($"Got schema version 1: {schemaVersion.Extensions.VersionId}, format: {schemaVersion.Extensions.Format}");
        }
        catch (EdgeRegistryServiceException e)
        {
            Console.WriteLine($"Get schema version 1 error: {e.ServiceError.Code} {e.ServiceError.Title}");
        }
    }

    // Create schema version 2
    ulong? versionId2 = null;
    try
    {
        SchemaVersion schemaVersion = await edgeRegistryClient.CreateSchemaVersionAsync(
            schemaId,
            new CreateSchemaVersionOptions
            {
                SchemaDocument = System.Text.Encoding.UTF8.GetBytes(jsonSchema),
                ContentType = "application/json",
                Name = "Example Schema Version 2",
                Description = "An example schema version",
                Format = SchemaFormat.JsonSchemaDraft07,
                Ancestor = versionId1,
            });
        Console.WriteLine($"Created schema version 2: {schemaVersion.Extensions.VersionId}");
        versionId2 = schemaVersion.Extensions.VersionId;
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Create schema version 2 error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Get schema after creating version 2
    try
    {
        Schema schema = await edgeRegistryClient.GetSchemaAsync(schemaId);
        Console.WriteLine($"Got schema: {schema.Resource.Id}, versions count: {schema.Resource.VersionsCount}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Get schema error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Get schema version 2
    if (versionId2 is not null)
    {
        try
        {
            SchemaVersion schemaVersion = await edgeRegistryClient.GetSchemaVersionAsync(schemaId, versionId2.Value);
            Console.WriteLine($"Got schema version 2: {schemaVersion.Extensions.VersionId}, format: {schemaVersion.Extensions.Format}");
        }
        catch (EdgeRegistryServiceException e)
        {
            Console.WriteLine($"Get schema version 2 error: {e.ServiceError.Code} {e.ServiceError.Title}");
        }
    }

    // List schema groups
    try
    {
        List<string> groups = await edgeRegistryClient.ListSchemaGroupsAsync();
        Console.WriteLine($"Schema groups: [{string.Join(", ", groups)}]");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"List schema groups error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // List schemas
    try
    {
        List<string> schemas = await edgeRegistryClient.ListSchemasAsync();
        Console.WriteLine($"Schemas: [{string.Join(", ", schemas)}]");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"List schemas error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // List schema versions
    try
    {
        List<ulong> versions = await edgeRegistryClient.ListSchemaVersionsAsync(schemaId);
        Console.WriteLine($"Schema versions: [{string.Join(", ", versions)}]");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"List schema versions error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // -----------------------------------------------------------------------
    // Thing Description Extension
    // -----------------------------------------------------------------------
    string thingDescriptionId = "exampleThingDescription2";
    string thingDescriptionVersionId1 = "1.0.1";
    string thingDescriptionVersionId2 = "1.5.3";

    // Create thing description group
    try
    {
        Group tdGroup = await edgeRegistryClient.CreateThingDescriptionGroupAsync(
            new GroupCreateAttributes { Name = "SDK Thing Descriptions" });
        Console.WriteLine($"Created thing description group: {tdGroup.Id}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Create thing description group error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Get thing description group
    try
    {
        Group tdGroup = await edgeRegistryClient.GetThingDescriptionGroupAsync();
        Console.WriteLine($"Got thing description group: {tdGroup.Id}, name: {tdGroup.Name}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Get thing description group error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Create thing description with initial version
    try
    {
        ThingDescription td = await edgeRegistryClient.CreateThingDescriptionAsync(
            thingDescriptionId,
            new CreateThingDescriptionVersionOptions
            {
                ThingDescriptionDocument = System.Text.Encoding.UTF8.GetBytes(thingDescription),
                VersionId = thingDescriptionVersionId1,
                ContentType = "application/json",
                Name = "Example Thing Description Version 1",
                Description = "An example thing description version",
                Format = ThingDescriptionFormat.WotTd11,
            });
        Console.WriteLine($"Created thing description: {td.Resource.Id}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Create thing description error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Get thing description
    try
    {
        ThingDescription td = await edgeRegistryClient.GetThingDescriptionAsync(thingDescriptionId);
        Console.WriteLine($"Got thing description: {td.Resource.Id}, versions count: {td.Resource.VersionsCount}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Get thing description error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Get thing description version 1
    try
    {
        ThingDescriptionVersion tdVersion = await edgeRegistryClient.GetThingDescriptionVersionAsync(
            thingDescriptionId, thingDescriptionVersionId1);
        Console.WriteLine($"Got thing description version 1: {tdVersion.Version.VersionId}, format: {tdVersion.Extensions.Format}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Get thing description version 1 error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Create thing description version 2
    try
    {
        ThingDescriptionVersion tdVersion = await edgeRegistryClient.CreateThingDescriptionVersionAsync(
            thingDescriptionId,
            new CreateThingDescriptionVersionOptions
            {
                ThingDescriptionDocument = System.Text.Encoding.UTF8.GetBytes(thingDescription),
                VersionId = thingDescriptionVersionId2,
                ContentType = "application/json",
                Name = "Example Thing Description Version 2",
                Description = "An example thing description version",
                Format = ThingDescriptionFormat.WotTd11,
                Ancestor = thingDescriptionVersionId1,
            });
        Console.WriteLine($"Created thing description version 2: {tdVersion.Version.VersionId}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Create thing description version 2 error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Get thing description after creating version 2
    try
    {
        ThingDescription td = await edgeRegistryClient.GetThingDescriptionAsync(thingDescriptionId);
        Console.WriteLine($"Got thing description: {td.Resource.Id}, versions count: {td.Resource.VersionsCount}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Get thing description error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // Get thing description version 2
    try
    {
        ThingDescriptionVersion tdVersion = await edgeRegistryClient.GetThingDescriptionVersionAsync(
            thingDescriptionId, thingDescriptionVersionId2);
        Console.WriteLine($"Got thing description version 2: {tdVersion.Version.VersionId}, format: {tdVersion.Extensions.Format}");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"Get thing description version 2 error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // List thing description groups
    try
    {
        List<string> groups = await edgeRegistryClient.ListThingDescriptionGroupsAsync();
        Console.WriteLine($"Thing description groups: [{string.Join(", ", groups)}]");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"List thing description groups error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // List thing descriptions
    try
    {
        List<string> tds = await edgeRegistryClient.ListThingDescriptionsAsync();
        Console.WriteLine($"Thing descriptions: [{string.Join(", ", tds)}]");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"List thing descriptions error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }

    // List thing description versions
    try
    {
        List<string> versions = await edgeRegistryClient.ListThingDescriptionVersionsAsync(thingDescriptionId);
        Console.WriteLine($"Thing description versions: [{string.Join(", ", versions)}]");
    }
    catch (EdgeRegistryServiceException e)
    {
        Console.WriteLine($"List thing description versions error: {e.ServiceError.Code} {e.ServiceError.Title}");
    }
}
finally
{
    await edgeRegistryClient.StopAsync();
}

Console.WriteLine("Done");
