// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Hosting;

namespace Azure.Iot.Operations.Connector
{
    public abstract class ConnectorBackgroundService : BackgroundService
    {
        public abstract Task RunConnectorAsync(CancellationToken cancellationToken = default);
    }
}
