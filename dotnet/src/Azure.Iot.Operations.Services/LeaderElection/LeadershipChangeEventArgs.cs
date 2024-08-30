namespace Azure.Iot.Operations.Services.LeaderElection
{
    public sealed class LeadershipChangeEventArgs : EventArgs
    {
        /// <summary>
        /// The new state of the leadership position.
        /// </summary>
        public LeadershipPositionState NewState { get; internal set; }

        /// <summary>
        /// The new leader. This value is null if this update is that the previous leader resigned or ended their term.
        /// </summary>
        public LeaderElectionCandidate? NewLeader { get; internal set; }

        /// <summary>
        /// The leader before this change. This value is null if there was no previous leader.
        /// </summary>
        public LeaderElectionCandidate? PreviousLeader { get; internal set; }

        internal LeadershipChangeEventArgs(LeaderElectionCandidate? newLeader)
        {
            NewLeader = newLeader;
        }
    }
}
