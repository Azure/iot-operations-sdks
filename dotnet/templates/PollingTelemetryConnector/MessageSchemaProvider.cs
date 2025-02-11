﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;

namespace PollingTelemetryConnector
{
    internal class MessageSchemaProvider : IMessageSchemaProvider
    {
        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(string datasetName, Dataset dataset, CancellationToken cancellationToken = default)
        {
            // By returning null, no message schema will be registered for telemetry sent for this dataset.
            return Task.FromResult((ConnectorMessageSchema?)null);
        }

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(string eventName, Event assetEvent, CancellationToken cancellationToken = default)
        {
            // By returning null, no message schema will be registered for telemetry sent for this event.
            return Task.FromResult((ConnectorMessageSchema?)null);
        }
    }
}
