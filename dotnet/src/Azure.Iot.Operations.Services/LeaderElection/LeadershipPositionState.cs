// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.LeaderElection
{
    /// <summary>
    /// The state of the leadership position
    /// </summary>
    public enum LeadershipPositionState
    {
        /// <summary>
        /// A leader was just elected.
        /// </summary>
        LeaderElected,

        /// <summary>
        /// A leader just resigned or their term just ended. A new leader can be elected at this time.
        /// </summary>
        NoLeader,
    }
}