// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Observability;

public class DefaultTimerFactory : ITimerFactory
{
    public ITimer CreateTimer()
    {
        return new TimerWrapper();
    }
}
