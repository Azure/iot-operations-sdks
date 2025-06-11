// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector
{
    internal class ConnectorDeviceTaskContext : IDisposable
    {
        public Task TaskToRunWhileDeviceIsAvailable { get; set; }

        public CancellationTokenSource TaskCancellationTokenSource { get; set; }

        public ConnectorDeviceTaskContext(Task taskToRunWhileDeviceIsAvailable)
        {
            TaskToRunWhileDeviceIsAvailable = taskToRunWhileDeviceIsAvailable;
            TaskCancellationTokenSource = new();
        }

        public void Dispose()
        {
            TaskCancellationTokenSource.Cancel();
            TaskCancellationTokenSource.Dispose();
        }
    }
}
