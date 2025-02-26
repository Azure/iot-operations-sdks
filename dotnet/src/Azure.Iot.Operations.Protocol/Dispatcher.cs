// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol
{
    internal delegate Task Dispatcher(Func<Task>? process, Func<Task> acknowledge);
}
