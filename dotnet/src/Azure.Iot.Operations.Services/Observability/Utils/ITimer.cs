// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Observability.Utils;

public interface ITimer : IAsyncDisposable
{
    void Start(Func<CancellationToken, Task> callback, CancellationToken cancellationToken, TimeSpan dueTime, TimeSpan period);
    Task StopAsync();
}
