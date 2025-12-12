// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Telemetry
{
    /// <summary>
    /// A CloudEvent that includes the DataContentType field.
    /// </summary>
    public class ExtendedCloudEvent : CloudEvent
    {
        /// <summary>
        ///  Content type of data value. This attribute enables data to carry any type of content, 
        ///  whereby format and encoding might differ from that of the chosen event format.
        /// </summary>
        public string? DataContentType { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtendedCloudEvent"/> class.
        /// </summary>
        /// <param name="source">The source of the event.</param>
        /// <param name="type">The type of the event.</param>
        /// <param name="specversion">The version of the CloudEvents specification.</param>
        public ExtendedCloudEvent(Uri source, string type = "ms.aio.telemetry", string specversion = "1.0")
            : base(source, type, specversion)
        {
        }
    }
}
