// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDForm : IEquatable<TDForm>, IDeserializable<TDForm>
    {
        public const string ContentTypeName = TDCommon.ContentTypeName;
        public const string AdditionalResponsesName = "additionalResponses";
        public const string HeaderInfoName = "dov:headerInfo";
        public const string HeaderInfoLegacyName = "dtv:headerInfo";
        public const string HeaderCodeName = "dov:headerCode";
        public const string HeaderCodeLegacyName = "dtv:headerCode";
        public const string ServiceGroupIdName = "dov:serviceGroupId";
        public const string ServiceGroupIdLegacyName = "dtv:serviceGroupId";
        public const string TopicName = "dov:topic";
        public const string TopicLegacyName = "dtv:topic";
        public const string HrefName = TDCommon.HrefName;
        public const string IncludeInheritedName = "dov:includeInherited";
        public const string IncludeInheritedLegacyName = "dtv:includeInherited";
        public const string OpName = "op";

        public static readonly HashSet<string> SupportedProperties = new()
        {
            ContentTypeName,
            AdditionalResponsesName,
            HeaderInfoName,
            HeaderInfoLegacyName,
            HeaderCodeName,
            HeaderCodeLegacyName,
            ServiceGroupIdName,
            ServiceGroupIdLegacyName,
            TopicName,
            TopicLegacyName,
            HrefName,
            IncludeInheritedName,
            IncludeInheritedLegacyName,
            OpName
        };

        public ValueTracker<StringHolder>? ContentType { get; set; }

        public ArrayTracker<TDSchemaReference>? AdditionalResponses { get; set; }

        public ArrayTracker<TDSchemaReference>? HeaderInfo { get; set; }

        public ValueTracker<StringHolder>? HeaderCode { get; set; }

        public ValueTracker<StringHolder>? ServiceGroupId { get; set; }

        public ValueTracker<StringHolder>? Topic { get; set; }

        public ValueTracker<StringHolder>? Href { get; set; }

        public ValueTracker<BoolHolder>? IncludeInherited { get; set; }

        public ArrayTracker<StringHolder>? Op { get; set; }

        public Dictionary<string, long> PropertyNames { get; set; } = new();

        public PrefixType HeaderInfoPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType HeaderCodePrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType ServiceGroupIdPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType TopicPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType IncludeInheritedPrefixType { get; set; } = PrefixType.Indeterminate;

        public virtual bool Equals(TDForm? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return ContentType == other.ContentType &&
                       AdditionalResponses == other.AdditionalResponses &&
                       HeaderInfo == other.HeaderInfo &&
                       HeaderCode == other.HeaderCode &&
                       ServiceGroupId == other.ServiceGroupId &&
                       Topic == other.Topic &&
                       Href == other.Href &&
                       IncludeInherited == other.IncludeInherited &&
                       Op == other.Op;
            }
        }

        public override int GetHashCode()
        {
            return (ContentType, AdditionalResponses, HeaderInfo, HeaderCode, ServiceGroupId, Topic, Href, IncludeInherited, Op).GetHashCode();
        }

        public static bool operator ==(TDForm? left, TDForm? right)
        {
            if (left is null)
            {
                return right is null;
            }
            else
            {
                return left.Equals(right);
            }
        }

        public static bool operator !=(TDForm? left, TDForm? right)
        {
            return !(left == right);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            else if (ReferenceEquals(obj, null))
            {
                return false;
            }
            else if (obj is not TDForm other)
            {
                return false;
            }
            else
            {
                return Equals(other);
            }
        }

        public IEnumerable<ITraversable> Traverse()
        {
            if (ContentType != null)
            {
                foreach (ITraversable item in ContentType.Traverse())
                {
                    yield return item;
                }
            }
            if (AdditionalResponses != null)
            {
                foreach (ITraversable item in AdditionalResponses.Traverse())
                {
                    yield return item;
                }
            }
            if (HeaderInfo != null)
            {
                foreach (ITraversable item in HeaderInfo.Traverse())
                {
                    yield return item;
                }
            }
            if (HeaderCode != null)
            {
                foreach (ITraversable item in HeaderCode.Traverse())
                {
                    yield return item;
                }
            }
            if (ServiceGroupId != null)
            {
                foreach (ITraversable item in ServiceGroupId.Traverse())
                {
                    yield return item;
                }
            }
            if (Topic != null)
            {
                foreach (ITraversable item in Topic.Traverse())
                {
                    yield return item;
                }
            }
            if (Href != null)
            {
                foreach (ITraversable item in Href.Traverse())
                {
                    yield return item;
                }
            }
            if (IncludeInherited != null)
            {
                foreach (ITraversable item in IncludeInherited.Traverse())
                {
                    yield return item;
                }
            }
            if (Op != null)
            {
                foreach (ITraversable item in Op.Traverse())
                {
                    yield return item;
                }
            }
        }

        public static TDForm Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDForm form = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                ParsingSupport.CheckForDuplicatePropertyName(ref reader, propertyName, form.PropertyNames, "form");
                form.PropertyNames[propertyName] = reader.TokenStartIndex;
                reader.Read();

                switch (propertyName)
                {
                    case ContentTypeName:
                        form.ContentType = ValueTracker<StringHolder>.Deserialize(ref reader, ContentTypeName);
                        break;
                    case AdditionalResponsesName:
                        form.AdditionalResponses = ArrayTracker<TDSchemaReference>.Deserialize(ref reader, AdditionalResponsesName);
                        break;
                    case HeaderInfoName:
                        form.HeaderInfo = ArrayTracker<TDSchemaReference>.Deserialize(ref reader, HeaderInfoName);
                        form.HeaderInfoPrefixType = PrefixType.DoVocabulary;
                        break;
                    case HeaderInfoLegacyName:
                        form.HeaderInfo = ArrayTracker<TDSchemaReference>.Deserialize(ref reader, HeaderInfoName);
                        form.HeaderInfoPrefixType = PrefixType.AioProtocol;
                        break;
                    case HeaderCodeName:
                        form.HeaderCode = ValueTracker<StringHolder>.Deserialize(ref reader, HeaderCodeName);
                        form.HeaderCodePrefixType = PrefixType.DoVocabulary;
                        break;
                    case HeaderCodeLegacyName:
                        form.HeaderCode = ValueTracker<StringHolder>.Deserialize(ref reader, HeaderCodeName);
                        form.HeaderCodePrefixType = PrefixType.AioProtocol;
                        break;
                    case ServiceGroupIdName:
                        form.ServiceGroupId = ValueTracker<StringHolder>.Deserialize(ref reader, ServiceGroupIdName);
                        form.ServiceGroupIdPrefixType = PrefixType.DoVocabulary;
                        break;
                    case ServiceGroupIdLegacyName:
                        form.ServiceGroupId = ValueTracker<StringHolder>.Deserialize(ref reader, ServiceGroupIdName);
                        form.ServiceGroupIdPrefixType = PrefixType.AioProtocol;
                        break;
                    case TopicName:
                        form.Topic = ValueTracker<StringHolder>.Deserialize(ref reader, TopicName);
                        form.TopicPrefixType = PrefixType.DoVocabulary;
                        break;
                    case TopicLegacyName:
                        form.Topic = ValueTracker<StringHolder>.Deserialize(ref reader, TopicName);
                        form.TopicPrefixType = PrefixType.AioProtocol;
                        break;
                    case HrefName:
                        form.Href = ValueTracker<StringHolder>.Deserialize(ref reader, HrefName);
                        break;
                    case IncludeInheritedName:
                        form.IncludeInherited = ValueTracker<BoolHolder>.Deserialize(ref reader, IncludeInheritedName);
                        form.IncludeInheritedPrefixType = PrefixType.DoVocabulary;
                        break;
                    case IncludeInheritedLegacyName:
                        form.IncludeInherited = ValueTracker<BoolHolder>.Deserialize(ref reader, IncludeInheritedName);
                        form.IncludeInheritedPrefixType = PrefixType.AioProtocol;
                        break;
                    case OpName:
                        form.Op = ArrayTracker<StringHolder>.Deserialize(ref reader, OpName);
                        break;
                    default:
                        reader.Skip();
                        break;
                }

                reader.Read();
            }

            return form;
        }
    }
}
