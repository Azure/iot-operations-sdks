// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


namespace Azure.Iot.Operations.Connector.Files.FilesMonitor
{
    public class PollingFilesMonitorFactory : IFilesMonitorFactory
    {
        public IFilesMonitor Create()
        {
            return new PollingFilesMonitor();
        }
    }
}
