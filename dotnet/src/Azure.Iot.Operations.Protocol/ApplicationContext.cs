using System;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// Application-wide context containing shared resources like the HybridLogicalClock.
    /// There should only be one instance per application, shared across all sessions.
    /// </summary>
    public class ApplicationContext : IAsyncDisposable
    {
        private bool _disposed;

        /// <summary>
        /// The HybridLogicalClock used by the application.
        /// </summary>
        public HybridLogicalClock ApplicationHlc { get; }

        /// <summary>
        /// Creates a new ApplicationContext with a HybridLogicalClock.
        /// </summary>
        public ApplicationContext(HybridLogicalClock? hybridLogicalClock = null)
        {
            ApplicationHlc = hybridLogicalClock ?? new();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await ApplicationHlc.DisposeAsync();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
