// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Per-asset runtime bookkeeping inside <see cref="ConnectorWorker"/>. Owns the long-lived
    /// <see cref="AssetClient"/> and two independent branch lifetimes:
    /// <list type="bullet">
    /// <item><b>MA branch</b> (<see cref="ManagementActionCts"/>/<see cref="ManagementActionTask"/>): the built-in management-action
    /// orchestrator. Cancelled <em>only</em> on Deleted, so handler state survives Updated events.</item>
    /// <item><b>User branch</b> (<see cref="UserCts"/>/<see cref="UserTask"/>): the
    /// <see cref="ConnectorWorker.WhileAssetIsAvailable"/> callback. Cancelled on Updated and Deleted;
    /// replaced via <see cref="SwapUserBranch"/> on Updated so it sees a fresh token.</item>
    /// </list>
    /// This context owns the <see cref="AssetClient"/>; <see cref="ManagementActionArgs"/> and <see cref="UserArgs"/>
    /// merely wrap it. Disposing this context disposes both args and then the AssetClient.
    /// </summary>
    internal sealed class AssetRuntimeContext : IAsyncDisposable
    {
        public AssetClient AssetClient { get; }
        public AssetAvailableEventArgs ManagementActionArgs { get; }
        public CancellationTokenSource ManagementActionCts { get; }
        public Task? ManagementActionTask { get; private set; }

        public CancellationTokenSource UserCts { get; private set; }
        public Task? UserTask { get; private set; }
        public AssetAvailableEventArgs? UserArgs { get; private set; }

        public AssetRuntimeContext(
            AssetClient assetClient,
            AssetAvailableEventArgs managementActionArgs,
            CancellationTokenSource managementActionCts,
            Task? managementActionTask,
            CancellationTokenSource userCts,
            Task? userTask,
            AssetAvailableEventArgs? userArgs)
        {
            AssetClient = assetClient;
            ManagementActionArgs = managementActionArgs;
            ManagementActionCts = managementActionCts;
            ManagementActionTask = managementActionTask;
            UserCts = userCts;
            UserTask = userTask;
            UserArgs = userArgs;
        }

        /// <summary>
        /// Attach the management-action branch task after the context is registered. The MA branch starts
        /// only once the per-asset slot is reserved, so a context that loses the race never writes to ADR.
        /// </summary>
        public void AttachManagementActionTask(Task managementActionTask) => ManagementActionTask = managementActionTask;

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
            try { ManagementActionCts.Dispose(); } catch (ObjectDisposedException) { }
            try { UserCts.Dispose(); } catch (ObjectDisposedException) { }

            if (UserArgs is not null)
            {
                try { await UserArgs.DisposeAsync(); } catch { /* best-effort */ }
            }

            try { await ManagementActionArgs.DisposeAsync(); } catch { /* best-effort */ }

            // This context owns the AssetClient; dispose it once both args are torn down.
            try { await AssetClient.DisposeAsync(); } catch { /* best-effort */ }
        }
    }
}
