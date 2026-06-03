// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Per-asset runtime bookkeeping inside <see cref="ConnectorWorker"/>. Owns the long-lived
    /// <see cref="AssetClient"/> and two independent branch lifetimes:
    /// <list type="bullet">
    /// <item><b>MA branch</b> (<see cref="MaCts"/>/<see cref="MaTask"/>): the built-in management-action
    /// orchestrator. Cancelled <em>only</em> on Deleted, so handler state survives Updated events.</item>
    /// <item><b>User branch</b> (<see cref="UserCts"/>/<see cref="UserTask"/>): the
    /// <see cref="ConnectorWorker.WhileAssetIsAvailable"/> callback. Cancelled on Updated and Deleted;
    /// replaced via <see cref="SwapUserBranch"/> on Updated so it sees a fresh token.</item>
    /// </list>
    /// <see cref="OwnedArgs"/> owns the <see cref="AssetClient"/>; <see cref="UserArgs"/> is borrow-mode.
    /// Disposing this context disposes both args and (through <see cref="OwnedArgs"/>) the AssetClient.
    /// </summary>
    internal sealed class AssetRuntimeContext : IAsyncDisposable
    {
        public AssetClient AssetClient => OwnedArgs.AssetClient;
        public AssetAvailableEventArgs OwnedArgs { get; }
        public CancellationTokenSource MaCts { get; }
        public Task? MaTask { get; private set; }

        public CancellationTokenSource UserCts { get; private set; }
        public Task? UserTask { get; private set; }
        public AssetAvailableEventArgs? UserArgs { get; private set; }

        public AssetRuntimeContext(
            AssetAvailableEventArgs ownedArgs,
            CancellationTokenSource maCts,
            Task? maTask,
            CancellationTokenSource userCts,
            Task? userTask,
            AssetAvailableEventArgs? userArgs)
        {
            OwnedArgs = ownedArgs;
            MaCts = maCts;
            MaTask = maTask;
            UserCts = userCts;
            UserTask = userTask;
            UserArgs = userArgs;
        }

        /// <summary>
        /// Attach the management-action branch task after the context is registered. The MA branch starts
        /// only once the per-asset slot is reserved, so a context that loses the race never writes to ADR.
        /// </summary>
        public void AttachMaTask(Task maTask) => MaTask = maTask;

        /// <summary>
        /// Atomically replace the user-branch trio after a tear-down + relaunch on Updated. Caller must
        /// first cancel/dispose the previous CTS, await the previous task, and dispose the previous args.
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
