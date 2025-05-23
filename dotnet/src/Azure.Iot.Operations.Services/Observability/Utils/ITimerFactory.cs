// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Observability.Utils;

public interface ITimerFactory
{
    ITimer CreateTimer();
}
