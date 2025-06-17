// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


namespace Azure.Iot.Operations.Connector.Files.FilesMonitor
{
    public class PollingFilesMonitorFactory : IFilesMonitorFactory
    {
        private readonly TimeSpan _pollingInterval;

        public PollingFilesMonitorFactory(TimeSpan? pollingInterval = null)
        {
            _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(1);
        }

        public IFilesMonitor Create()
        {
            return new PollingFilesMonitor(_pollingInterval);
        }
    }
}
