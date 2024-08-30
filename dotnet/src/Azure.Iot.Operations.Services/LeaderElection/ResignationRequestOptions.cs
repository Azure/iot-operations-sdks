namespace Azure.Iot.Operations.Services.LeaderElection
{
    public class ResignationRequestOptions
    {
        /// <summary>
        /// If true, this operation will also stop any auto-renewing configured by <see cref="LeaderElectionClient.AutomaticRenewalOptions"/>.
        /// If false, any auto-renewing will continue as-is.
        /// </summary>
        /// <remarks>
        /// By default, auto-renewal will be cancelled.
        /// </remarks>
        public bool CancelAutomaticRenewal { get; set; } = true;

        /// <summary>
        /// The optional value to include in the lock's value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only provide this value if the sessionId was set when campaigning to be leader in <see cref="CampaignRequestOptions.SessionId"/>.
        /// If the sessionId was set when campaigning, but not when resigning (or vice versa), then
        /// attempts to resign will fail.
        /// </para>
        /// <para>
        /// By providing a unique sessionId, an application can use the same candidate name and/or the same MQTT client
        /// in different threads to campaign to be leader on the same lock without worrying about accidentally allowing two clients
        /// to both be leader at the same time.
        /// </para>
        /// </remarks>
        public string? SessionId { get; set; }
    }
}