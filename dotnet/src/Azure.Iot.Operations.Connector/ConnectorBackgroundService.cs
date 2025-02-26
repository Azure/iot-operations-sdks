// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Hosting;

namespace Azure.Iot.Operations.Connector
{
    public abstract class ConnectorBackgroundService : BackgroundService
    {
        /// <summary>
        /// Run the connector worker.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This will run the connector until the provided cancellation token is cancelled.
        /// </remarks>
        public abstract Task RunConnectorAsync(CancellationToken cancellationToken = default);
    }
}
