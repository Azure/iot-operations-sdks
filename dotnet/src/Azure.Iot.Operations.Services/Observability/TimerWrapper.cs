// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Observability
{
    public class TimerWrapper : ITimer
    {
        private Timer? _timer;
        private bool _isDisposed;

        public void Start(Func<CancellationToken, Task> callback, CancellationToken cancellationToken, TimeSpan dueTime, TimeSpan period)
        {
            _timer = new Timer(
                async _ => await callback(CancellationToken.None),
                null,
                dueTime,
                period);
        }

        public async Task StopAsync()
        {
            if (_timer != null)
            {
                await _timer.DisposeAsync();
                _timer = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            await StopAsync();
            _isDisposed = true;
        }
    }
}
