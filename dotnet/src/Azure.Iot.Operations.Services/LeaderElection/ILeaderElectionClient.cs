// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.StateStore;

namespace Azure.Iot.Operations.Services.LeaderElection
{
    /// <summary>
    /// The interface for clients that perform leader election.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Once elected, a client will not automatically renew its position by default. This client allows you to opt-in to auto-renew
    /// with <see cref="AutomaticRenewalOptions"/>, though.
    /// </para>
    /// </remarks>
    public interface ILeaderElectionClient : IAsyncDisposable
    {
        /// <summary>
        /// The callback that executes whenever the current leader changes.
        /// </summary>
        /// <remarks>
        /// Users who want to watch leadership change events must first set this callback, then
        /// call <see cref="ObserveLeadershipChangesAsync(ObserveLeadershipChangesRequestOptions?, CancellationToken)"/>.
        /// To stop watching leadership change events, call <see cref="UnobserveLeadershipChangesAsync(CancellationToken)"/>
        /// and then remove any handlers from this object.
        /// </remarks>
        event Func<object?, LeadershipChangeEventArgs, Task>? LeadershipChangeEventReceivedAsync;

        /// <summary>
        /// The name of this client that is used when campaigning to be leader.
        /// </summary>
        string CandidateName { get; }

        /// <summary>
        /// The options for automatically re-campaigning to be leader at the end of a term as leader.
        /// By default, no automatic renewing happens.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These options must be set before calling <see cref="CampaignAsync(TimeSpan, CampaignRequestOptions?, CancellationToken)"/>.
        /// Once set, the automatic renewal will begin after the first call to <see cref="CampaignAsync(TimeSpan, CampaignRequestOptions?, CancellationToken)"/>.
        /// </para>
        /// <para>
        /// Automatic renewal will continue for as long as the leadership position can be re-acquired. If another party acquires the leadership position, then this party's auto-renewal
        /// will end. In this case, users should use <see cref="CampaignAsync(TimeSpan, CampaignRequestOptions?, CancellationToken)"/> to campaign
        /// instead to avoid polling.
        /// </para>
        /// <para>
        /// The result of automatic renewals can be accessed via <see cref="LastKnownCampaignResult"/>.
        /// </para>
        /// </remarks>
        LeaderElectionAutomaticRenewalOptions AutomaticRenewalOptions { get; set; }

        /// <summary>
        /// The result of the most recently run campaign.
        /// </summary>
        /// <remarks>
        /// This value captures the result of automatic re-campaigning with <see cref="AutomaticRenewalOptions"/>.
        /// </remarks>
        CampaignResponse? LastKnownCampaignResult { get; }

        /// <summary>
        /// Make a single attempt to campaign to be leader.
        /// </summary>
        /// <param name="electionTerm">How long the client will be leader if elected. This value only has millisecond-level precision.</param>
        /// <param name="options">The optional parameters for this request.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the campaign.</returns>
        /// <remarks>
        /// <para>
        /// Once elected, this client will not automatically renew its position by default. This client allows you to opt-in to auto-renew
        /// with <see cref="AutomaticRenewalOptions"/>, though.
        /// </para>
        /// </remarks>
        Task<CampaignResponse> TryCampaignAsync(TimeSpan electionTerm, CampaignRequestOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Await until this client is elected leader or cancellation is requested.
        /// </summary>
        /// <param name="electionTerm">How long the client will be leader if elected. This value only has millisecond-level precision.</param>
        /// <param name="options">The optional parameters for this request.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the campaign.</returns>
        /// <remarks>
        /// <para>
        /// Once elected, this client will not automatically renew its position by default. This client allows you to opt-in to auto-renew
        /// with <see cref="AutomaticRenewalOptions"/>, though.
        /// </para>
        /// </remarks>
        Task<CampaignResponse> CampaignAsync(TimeSpan electionTerm, CampaignRequestOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Block until elected leader, update the value of the state store resource based on
        /// its current value, then resign.
        /// </summary>
        /// <param name="key">The state store key whose value will be updated.</param>
        /// <param name="updateValueFunc">
        /// The function to execute after elected leader. The parameter of this function contains
        /// the current value of the state store key. The return value of this function is the new value
        /// that you wish the state store key to have.
        /// </param>
        /// <param name="maximumTermLength">
        /// The maximum length of time that the client will be leader once elected. Under normal circumstances,
        /// this function will resign from the leadership position after updating the value of the shared resource, but
        /// it is possible that this client is interrupted or encounters a fatal exception. By setting a low value for this field,
        /// you limit how long the leadership position can be acquired for before it is released automatically by the service.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This function will always resign from the leadership position if it was elected. Even if cancellation is requested
        /// after being elected leader, this function will resign from that position.
        /// </remarks>
        Task CampaignAndUpdateValueAsync(StateStoreKey key, Func<StateStoreValue?, StateStoreValue?> updateValueFunc, TimeSpan? maximumTermLength = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the name of the current leader.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The details about the current leader.</returns>
        Task<GetCurrentLeaderResponse> GetCurrentLeaderAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resign from being the leader.
        /// </summary>
        /// <param name="options">The optional parameters for this request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of the attempted resignation.</returns>
        Task<ResignationResponse> ResignAsync(ResignationRequestOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Start receiving notifications when the leader changes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// Users who want to watch lock holder change events must first set one or more handlers on
        /// <see cref="LeadershipChangeEventReceivedAsync"/>, then call this function.
        /// To stop watching lock holder change events, call <see cref="UnobserveLeadershipChangesAsync(CancellationToken)"/>
        /// and then remove any handlers from <see cref="LeadershipChangeEventReceivedAsync"/>.
        /// </remarks>
        Task ObserveLeadershipChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop receiving notifications when the leader changes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// Users who want to watch lock holder change events must first set one or more handlers on
        /// <see cref="LeadershipChangeEventReceivedAsync"/>, then call <see cref="ObserveLeadershipChangesAsync(ObserveLeadershipChangesRequestOptions?, CancellationToken)"/>.
        /// To stop watching lock holder change events, call this function
        /// and then remove any handlers from <see cref="LeadershipChangeEventReceivedAsync"/>.
        /// </remarks>
        Task UnobserveLeadershipChangesAsync(CancellationToken cancellationToken = default);
    }
}
