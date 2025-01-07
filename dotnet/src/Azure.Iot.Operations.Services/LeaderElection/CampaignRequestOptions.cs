// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.LeaderElection
{
    public class CampaignRequestOptions
    {
        /// <summary>
        /// The optional value to include in the lock's value. If not provided, the lock's value will equal
        /// the candidate's name. If it is provided, the lock's value will equal {candidate name}:{sessionId}
        /// </summary>
        /// <remarks>
        /// <para>
        /// If this value is provided, then you'll need to provide the same value to <see cref="ResignationRequestOptions.SessionId"/>
        /// or attempts to resign will fail.
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