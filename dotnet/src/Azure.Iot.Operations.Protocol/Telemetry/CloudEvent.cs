using System;

namespace Azure.Iot.Operations.Protocol.Telemetry;

/// <summary>
/// Implements the CloudEvent spec 1.0. The required fields are source, type, id and specversion.
/// Id is required but we want to update it in the same instance. 
/// See <a href="https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md">CloudEvent Spec</a>
/// </summary>
/// <param name="source"><see cref="Source"/></param>
/// <param name="type"><see cref="Type"/></param>
/// <param name="specversion"><see cref="SpecVersion"/></param>
public class CloudEvent(Uri source, string type = "ms.aio.telemetry", string specversion = "1.0")
{
    private string? _id = null!;

    /// <summary>
    /// Identifies the context in which an event happened.
    /// Often this will include information such as the type of the event source, 
    /// the organization publishing the event or the process that produced the event. 
    /// The exact syntax and semantics behind the data encoded in the URI is defined by the event producer.
    /// </summary>
    public Uri? Source { get => source; }

    /// <summary>The version of the CloudEvents specification which the event uses. 
    /// This enables the interpretation of the context. 
    /// Compliant event producers MUST use a value of 1.0 when referring to this version of the specification.
    /// </summary>
    public string SpecVersion { get => specversion;  }

    /// <summary>
    /// Contains a value describing the type of event related to the originating occurrence. 
    /// Often this attribute is used for routing, observability, policy enforcement, etc. 
    /// The format of this is producer defined and might include information such as the version of the type
    /// </summary>
    public string Type { get => type;  }

    /// <summary>
    ///  Identifies the event. Producers MUST ensure that source + id is unique for each distinct event. 
    ///  If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same id. 
    ///  Consumers MAY assume that Events with identical source and id are duplicates.
    /// </summary>
    public string? Id { get => _id; internal set => _id = value; } // although id is required, we want update it in the same instance from the sender.

    // optional 
    private string? _dataContentType;
    private string? _dataSchema;
    private string? _subject;
    private DateTime? _time;

    /// <summary>
    /// Timestamp of when the occurrence happened. 
    /// If the time of the occurrence cannot be determined then this attribute MAY be set to some other time (such as the current time) 
    /// by the CloudEvents producer, 
    /// however all producers for the same source MUST be consistent in this respect. 
    /// </summary>
    public DateTime? Time { get => _time; internal set => _time = value; }

    /// <summary>
    ///  Content type of data value. This attribute enables data to carry any type of content, 
    ///  whereby format and encoding might differ from that of the chosen event format.
    /// </summary>
    public string? DataContentType { get => _dataContentType; internal set => _dataContentType = value; }

    /// <summary>
    /// Identifies the subject of the event in the context of the event producer (identified by source). 
    /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source, 
    /// but the source identifier alone might not be sufficient as a qualifier for any specific event if the source context has internal sub-structure.
    /// </summary>
    public string? Subject { get => _subject; internal set => _subject = value; }

    /// <summary>
    ///  Identifies the schema that data adheres to. 
    ///  Incompatible changes to the schema SHOULD be reflected by a different URI.
    /// </summary>
    public string? DataSchema { get => _dataSchema; set => _dataSchema = value; }
}
