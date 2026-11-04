// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.CloudEvents;

internal enum CloudEventValueKind
{
    Automatic,
    AutomaticWithProtocolSpecificIdentifier,
    PublishTopic,
    Custom,
    None,
}

/// <summary>
/// Determines the <c>source</c> header of a Cloud Event.
/// </summary>
public sealed class CloudEventSource
{
    private CloudEventSource(CloudEventValueKind kind, string? protocolSpecificIdentifier = null, Uri? customSource = null)
    {
        Kind = kind;
        ProtocolSpecificIdentifier = protocolSpecificIdentifier;
        CustomSource = customSource;
    }

    internal CloudEventValueKind Kind { get; }

    internal string? ProtocolSpecificIdentifier { get; }

    internal Uri? CustomSource { get; }

    /// <summary>
    /// <c>ms-aio:&lt;device identifier&gt;[/&lt;data source&gt;]</c>, where the device identifier is the device's
    /// external ID if it is set and differs from the device's UUID, otherwise the device name, and the data source
    /// is the dataset's or event's <c>dataSource</c>, without leading <c>/</c>s, if it is set and forms a valid source.
    /// </summary>
    public static CloudEventSource Automatic { get; } = new(CloudEventValueKind.Automatic);

    /// <summary>
    /// <c>ms-aio:&lt;identifier&gt;[/&lt;data source&gt;]</c>, where the identifier is the trimmed
    /// <paramref name="protocolSpecificIdentifier"/> and the data source is the same as for <see cref="Automatic"/>.
    /// The inbound endpoint address is a recommended identifier.
    /// </summary>
    /// <param name="protocolSpecificIdentifier">The protocol specific identifier of the device.</param>
    /// <exception cref="ArgumentException">If the trimmed identifier is empty or doesn't form a valid source.</exception>
    public static CloudEventSource AutomaticWithProtocolSpecificIdentifier(string protocolSpecificIdentifier)
    {
        ArgumentNullException.ThrowIfNull(protocolSpecificIdentifier);
        string trimmedIdentifier = protocolSpecificIdentifier.Trim();
        if (trimmedIdentifier.Length == 0 || !Uri.IsWellFormedUriString($"ms-aio:{trimmedIdentifier}", UriKind.Absolute))
        {
            throw new ArgumentException($"Protocol specific identifier '{protocolSpecificIdentifier}' does not form a valid source.", nameof(protocolSpecificIdentifier));
        }

        return new(CloudEventValueKind.AutomaticWithProtocolSpecificIdentifier, protocolSpecificIdentifier: trimmedIdentifier);
    }

    /// <summary>
    /// The provided <c>source</c>, used as is.
    /// </summary>
    /// <param name="source">The source. Must be a non-empty URI-reference.</param>
    /// <exception cref="ArgumentException">If <paramref name="source"/> is empty.</exception>
    public static CloudEventSource Custom(Uri source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrEmpty(source.OriginalString))
        {
            throw new ArgumentException("Source must be a non-empty URI-reference.", nameof(source));
        }

        return new(CloudEventValueKind.Custom, customSource: source);
    }
}

/// <summary>
/// Determines the <c>type</c> header of a Cloud Event.
/// </summary>
public sealed class CloudEventType
{
    private CloudEventType(CloudEventValueKind kind, string? value = null)
    {
        Kind = kind;
        Value = value;
    }

    internal CloudEventValueKind Kind { get; }

    internal string? Value { get; }

    /// <summary>
    /// <c>&lt;kind&gt;[/&lt;type ref&gt;]</c>, where the kind is <c>DataSet</c> or <c>Event</c> and the type ref is
    /// the dataset's or event's <c>typeRef</c>, if any.
    /// </summary>
    public static CloudEventType Automatic { get; } = new(CloudEventValueKind.Automatic);

    /// <summary>
    /// The provided <c>type</c>.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <exception cref="ArgumentException">If <paramref name="type"/> is empty or whitespace.</exception>
    public static CloudEventType Custom(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return new(CloudEventValueKind.Custom, type);
    }
}

/// <summary>
/// Determines the <c>subject</c> header of a Cloud Event.
/// </summary>
public sealed class CloudEventSubject
{
    private CloudEventSubject(CloudEventValueKind kind, string? value = null)
    {
        Kind = kind;
        Value = value;
    }

    internal CloudEventValueKind Kind { get; }

    internal string? Value { get; }

    /// <summary>
    /// <c>&lt;asset identifier&gt;/&lt;component name&gt;</c>, where the asset identifier is the asset's external ID
    /// if it is set and differs from the asset's UUID, otherwise the asset name, and the component name is the
    /// dataset name, or <c>&lt;event group name&gt;/&lt;event name&gt;</c> for an event.
    /// </summary>
    public static CloudEventSubject Automatic { get; } = new(CloudEventValueKind.Automatic);

    /// <summary>
    /// The topic the message is published to.
    /// </summary>
    public static CloudEventSubject PublishTopic { get; } = new(CloudEventValueKind.PublishTopic);

    /// <summary>
    /// No <c>subject</c>.
    /// </summary>
    public static CloudEventSubject None { get; } = new(CloudEventValueKind.None);

    /// <summary>
    /// The provided <c>subject</c>.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <exception cref="ArgumentException">If <paramref name="subject"/> is empty or whitespace.</exception>
    public static CloudEventSubject Custom(string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        return new(CloudEventValueKind.Custom, subject);
    }
}

/// <summary>
/// Determines the <c>dataschema</c> header of a Cloud Event.
/// </summary>
public sealed class CloudEventDataSchema
{
    private CloudEventDataSchema(CloudEventValueKind kind, string? value = null)
    {
        Kind = kind;
        Value = value;
    }

    internal CloudEventValueKind Kind { get; }

    internal string? Value { get; }

    /// <summary>
    /// <c>aio-sr://&lt;registry namespace&gt;/&lt;schema name&gt;:&lt;schema version&gt;</c> for the message schema
    /// registered for the dataset or event, or no <c>dataschema</c> if one hasn't been registered.
    /// </summary>
    public static CloudEventDataSchema Automatic { get; } = new(CloudEventValueKind.Automatic);

    /// <summary>
    /// No <c>dataschema</c>.
    /// </summary>
    public static CloudEventDataSchema None { get; } = new(CloudEventValueKind.None);

    /// <summary>
    /// The provided <c>dataschema</c>.
    /// </summary>
    /// <param name="dataSchema">The data schema. Must be an absolute URI.</param>
    /// <exception cref="ArgumentException">If <paramref name="dataSchema"/> isn't a well-formed absolute URI.</exception>
    public static CloudEventDataSchema Custom(string dataSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSchema);
        if (!Uri.IsWellFormedUriString(dataSchema, UriKind.Absolute))
        {
            throw new ArgumentException($"Data schema '{dataSchema}' is not a well-formed absolute URI.", nameof(dataSchema));
        }

        return new(CloudEventValueKind.Custom, dataSchema);
    }
}

/// <summary>
/// Determines the <c>id</c> header of a Cloud Event.
/// </summary>
public sealed class CloudEventId
{
    private CloudEventId(CloudEventValueKind kind, string? value = null)
    {
        Kind = kind;
        Value = value;
    }

    internal CloudEventValueKind Kind { get; }

    internal string? Value { get; }

    /// <summary>
    /// A new GUID.
    /// </summary>
    public static CloudEventId Automatic { get; } = new(CloudEventValueKind.Automatic);

    /// <summary>
    /// The provided <c>id</c>.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <exception cref="ArgumentException">If <paramref name="id"/> is empty or whitespace.</exception>
    public static CloudEventId Custom(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return new(CloudEventValueKind.Custom, id);
    }
}

/// <summary>
/// Determines the <c>time</c> header of a Cloud Event.
/// </summary>
public sealed class CloudEventTime
{
    private CloudEventTime(CloudEventValueKind kind, DateTime? value = null)
    {
        Kind = kind;
        Value = value;
    }

    internal CloudEventValueKind Kind { get; }

    internal DateTime? Value { get; }

    /// <summary>
    /// The current time of the application's hybrid logical clock when the data is forwarded.
    /// </summary>
    public static CloudEventTime Automatic { get; } = new(CloudEventValueKind.Automatic);

    /// <summary>
    /// No <c>time</c>.
    /// </summary>
    public static CloudEventTime None { get; } = new(CloudEventValueKind.None);

    /// <summary>
    /// The provided <c>time</c>.
    /// </summary>
    /// <param name="time">The time. Must be UTC or local time.</param>
    /// <exception cref="ArgumentException">If <paramref name="time"/> has <see cref="DateTimeKind.Unspecified"/> kind.</exception>
    public static CloudEventTime Custom(DateTime time)
    {
        if (time.Kind == DateTimeKind.Unspecified)
        {
            throw new ArgumentException("Time must be UTC or local time so it can be serialized with a time zone offset.", nameof(time));
        }

        return new(CloudEventValueKind.Custom, time);
    }
}

/// <summary>
/// Determines the <c>aiodeviceref</c> header of a Cloud Event.
/// </summary>
public sealed class CloudEventAioDeviceRef
{
    private CloudEventAioDeviceRef(CloudEventValueKind kind, string? value = null)
    {
        Kind = kind;
        Value = value;
    }

    internal CloudEventValueKind Kind { get; }

    internal string? Value { get; }

    /// <summary>
    /// <c>ms-aio:&lt;device UUID&gt;_&lt;inbound endpoint name&gt;</c>. The device must have a UUID.
    /// </summary>
    public static CloudEventAioDeviceRef Automatic { get; } = new(CloudEventValueKind.Automatic);

    /// <summary>
    /// No <c>aiodeviceref</c>.
    /// </summary>
    public static CloudEventAioDeviceRef None { get; } = new(CloudEventValueKind.None);

    /// <summary>
    /// The provided <c>aiodeviceref</c>.
    /// </summary>
    /// <param name="aioDeviceRef">The aiodeviceref.</param>
    /// <exception cref="ArgumentException">If <paramref name="aioDeviceRef"/> is empty or whitespace.</exception>
    public static CloudEventAioDeviceRef Custom(string aioDeviceRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aioDeviceRef);
        return new(CloudEventValueKind.Custom, aioDeviceRef);
    }
}

/// <summary>
/// Determines the <c>aioassetref</c> header of a Cloud Event.
/// </summary>
public sealed class CloudEventAioAssetRef
{
    private CloudEventAioAssetRef(CloudEventValueKind kind, string? value = null)
    {
        Kind = kind;
        Value = value;
    }

    internal CloudEventValueKind Kind { get; }

    internal string? Value { get; }

    /// <summary>
    /// <c>ms-aio:&lt;asset UUID&gt;</c>. The asset must have a UUID.
    /// </summary>
    public static CloudEventAioAssetRef Automatic { get; } = new(CloudEventValueKind.Automatic);

    /// <summary>
    /// No <c>aioassetref</c>.
    /// </summary>
    public static CloudEventAioAssetRef None { get; } = new(CloudEventValueKind.None);

    /// <summary>
    /// The provided <c>aioassetref</c>.
    /// </summary>
    /// <param name="aioAssetRef">The aioassetref.</param>
    /// <exception cref="ArgumentException">If <paramref name="aioAssetRef"/> is empty or whitespace.</exception>
    public static CloudEventAioAssetRef Custom(string aioAssetRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aioAssetRef);
        return new(CloudEventValueKind.Custom, aioAssetRef);
    }
}
