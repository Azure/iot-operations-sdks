// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.LeaderElection
{
    public class ResignationResponse
    {
        /// <summary>
        /// True if the lock was successfully released and false otherwise.
        /// </summary>
        public bool Success { get; internal set; }

        internal ResignationResponse(bool success)
        {
            Success = success;
        }
    }
}
