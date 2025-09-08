// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector
{
    internal class UserTaskContext
    {
        internal Task UserTask { get; set; }

        internal CancellationTokenSource CancellationTokenSource { get; set; }

        internal UserTaskContext(Task userTask, CancellationTokenSource cancellationTokenSource)
        {
            CancellationTokenSource = cancellationTokenSource;
            UserTask = userTask;
        }
    }
}
