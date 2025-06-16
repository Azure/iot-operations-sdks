// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector.Files.FilesMonitor
{
    public interface IFilesMonitorFactory
    {
        IFilesMonitor Create();
    }
}
