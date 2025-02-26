// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.LeaderElection
{
    public class ObserveLeadershipChangesRequestOptions
    {
        /// <summary>
        /// If true, notifications about this leadership position changing will include the new leader after the change.
        /// If false, notifications about this lock changing will not include the new leader.
        /// </summary>
        /// <remarks>
        /// The new value will be set in <see cref="LeadershipChangeEventArgs.NewLeader"/>
        /// </remarks>
        public bool GetNewLeader { get; set; } = false;
    }
}
