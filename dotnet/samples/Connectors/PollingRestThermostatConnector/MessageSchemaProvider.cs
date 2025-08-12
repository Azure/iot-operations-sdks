// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace PollingRestThermostatConnector
{
    public class MessageSchemaProvider : IMessageSchemaProvider
    {
        private static readonly string _datasetJsonSchema = """
    {
        "$schema": "https://json-schema.org/draft-07/schema#",
        "type": "object",
        "properties": {
      	  "desired": {
        	    "type": "number"
        	},
        	"current": {
            	"type": "number"
        	}
        }
    }
    """;

        public static Func<IServiceProvider, IMessageSchemaProvider> Factory = service =>
        {
            return new MessageSchemaProvider();
        };

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(Device device, Asset asset, string datasetName, AssetDataset dataset, CancellationToken cancellationToken = default)
        {
            if (datasetName.Equals("thermostat_status", StringComparison.OrdinalIgnoreCase))
            {
                ConnectorMessageSchema? schema = new ConnectorMessageSchema(_datasetJsonSchema, Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry.Format.JsonSchemaDraft07, Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry.SchemaType.MessageSchema, "1.0", null);
                return Task.FromResult((ConnectorMessageSchema?) schema);
            }

            return Task.FromResult((ConnectorMessageSchema?) null);
        }

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(Device device, Asset asset, string eventName, AssetEvent assetEvent, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
