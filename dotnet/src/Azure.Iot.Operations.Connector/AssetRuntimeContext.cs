// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Per-asset runtime bookkeeping inside <see cref="ConnectorWorker"/>. Owns the long-lived
    /// <see cref="AssetClient"/> and the two independent branch lifetimes that run while an asset
    /// is available:
    /// <list type="bullet">
    /// <item><b>MA branch</b> (<see cref="MaCts"/>/<see cref="MaTask"/>) runs the built-in
    /// management-action orchestrator. Cancelled <em>only</em> on Deleted, so handler state
    /// survives across asset Updated events.</item>
    /// <item><b>User branch</b> (<see cref="UserCts"/>/<see cref="UserTask"/>) runs the
    /// user-supplied <see cref="ConnectorWorker.WhileAssetIsAvailable"/> callback. Cancelled
    /// on both Updated and Deleted; replaced via <see cref="SwapUserBranch"/> on Updated so the
    /// user callback sees a fresh <see cref="CancellationToken"/>.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <see cref="OwnedArgs"/> owns the <see cref="AssetClient"/>; <see cref="UserArgs"/> is a
    /// borrow-mode args instance that does not. Disposing this context disposes both args and
    /// (through <see cref="OwnedArgs"/>) the <see cref="AssetClient"/>.
    /// </remarks>
    internal sealed class AssetRuntimeContext : IAsyncDisposable
    {
        public AssetClient AssetClient { get; }
        public AssetAvailableEventArgs OwnedArgs { get; }
        public CancellationTokenSource MaCts { get; }
        public Task? MaTask { get; private set; }

        public CancellationTokenSource UserCts { get; private set; }
        public Task? UserTask { get; private set; }
        public AssetAvailableEventArgs? UserArgs { get; private set; }

        public AssetRuntimeContext(
            AssetClient assetClient,
            AssetAvailableEventArgs ownedArgs,
            CancellationTokenSource maCts,
            Task? maTask,
            CancellationTokenSource userCts,
            Task? userTask,
            AssetAvailableEventArgs? userArgs)
        {
            AssetClient = assetClient;
            OwnedArgs = ownedArgs;
            MaCts = maCts;
            MaTask = maTask;
            UserCts = userCts;
            UserTask = userTask;
            UserArgs = userArgs;
        }

        /// <summary>
        /// Attach the management-action branch task after the runtime context has been registered
        /// in the worker's tracking dictionary. The MA branch is started only once the per-asset
        /// slot has been reserved, so a context that loses the reservation race never starts a
        /// branch (and therefore can never write to / clobber ADR).
        /// </summary>
        public void AttachMaTask(Task maTask) => MaTask = maTask;

        /// <summary>
        /// Atomically replace the user-branch trio after a successful tear-down + relaunch on
        /// asset Updated. Caller is responsible for cancelling/disposing the previous CTS,
        /// awaiting the previous task, and disposing the previous user args before invoking.
        /// </summary>
        public void SwapUserBranch(CancellationTokenSource newUserCts, Task? newUserTask, AssetAvailableEventArgs? newUserArgs)
        {
            UserCts = newUserCts;
            UserTask = newUserTask;
            UserArgs = newUserArgs;
        }

        public async ValueTask DisposeAsync()
        {
            try { MaCts.Dispose(); } catch (ObjectDisposedException) { }
            try { UserCts.Dispose(); } catch (ObjectDisposedException) { }

            if (UserArgs is not null)
            {
                try { await UserArgs.DisposeAsync(); } catch { /* best-effort */ }
            }

            // OwnedArgs disposes the AssetClient (it was constructed in owns=true mode).
            try { await OwnedArgs.DisposeAsync(); } catch { /* best-effort */ }
        }
    }
}
