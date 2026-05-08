// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.SchemaRegistry.Models
{
    /// <summary>
    /// Class for converting from generated types to wrapped types
    /// </summary>
    internal class Converter
    {
        internal static Models.SchemaRegistryErrorCode toModel(int generated)
        {
            switch (generated)
            {
                case 400:
                    return Models.SchemaRegistryErrorCode.BadRequest;
                case 404:
                    return Models.SchemaRegistryErrorCode.NotFound;
                case 500:
                    return Models.SchemaRegistryErrorCode.InternalError;
                default:
                    // should never happen
                    throw new ArgumentException("Received an unexpected schema registry error code: " + generated);
            }
        }


        internal static Models.SchemaRegistryErrorDetails toModel(Azure.Iot.Operations.Services.SchemaRegistry.Generated.SchemaRegistryErrorDetails generated)
        {
            return new()
            {
                Code = generated.Code,
                CorrelationId = generated.CorrelationId,
                Message = generated.Message,
            };
        }


        internal static Models.SchemaRegistryErrorException toModel(Azure.Iot.Operations.Services.SchemaRegistry.Generated.SchemaRegistryErrorException generated)
        {
            // This includes setting the inner exception as the generated exception
            return new(toModel(generated.SchemaRegistryError), generated)
            {
                SchemaRegistryError = toModel(generated.SchemaRegistryError),
            };
        }

        internal static Models.SchemaRegistryError toModel(Azure.Iot.Operations.Services.SchemaRegistry.Generated.SchemaRegistryError generated)
        {
            SchemaRegistryErrorTarget? target = null;
            if (generated.Target != null)
            {
                switch (generated.Target)
                {
                    case "VersionProperty":
                        target = SchemaRegistryErrorTarget.VersionProperty; break;
                    case "DescriptionProperty":
                        target = SchemaRegistryErrorTarget.DescriptionProperty; break;
                    case "DisplayNameProperty":
                        target = SchemaRegistryErrorTarget.DisplayNameProperty; break;
                    case "FormatProperty":
                        target = SchemaRegistryErrorTarget.FormatProperty; break;
                    case "NameProperty":
                        target = SchemaRegistryErrorTarget.NameProperty; break;
                    case "SchemaArmResource":
                        target = SchemaRegistryErrorTarget.SchemaArmResource; break;
                    case "SchemaContentProperty":
                        target = SchemaRegistryErrorTarget.SchemaContentProperty; break;
                    case "SchemaRegistryArmResource":
                        target = SchemaRegistryErrorTarget.SchemaRegistryArmResource; break;
                    case "SchemaTypeProperty":
                        target = SchemaRegistryErrorTarget.SchemaTypeProperty; break;
                    case "SchemaVersionArmResource":
                        target = SchemaRegistryErrorTarget.SchemaVersionArmResource; break;
                    case "TagsProperty":
                        target = SchemaRegistryErrorTarget.TagsProperty; break;
                    default:
                        throw new ArgumentException("Received unexpected target field: " + generated.Target);
                }
            }

            return new()
            {
                Code = toModel(generated.Code),
                Details = generated.Details != null ? toModel(generated.Details) : null,
                InnerError = generated.InnerError != null ? toModel(generated.InnerError) : null,
                Message = generated.Message,
                Target = target,
            };
        }

        internal static SchemaRegistry.Schema toModel(Azure.Iot.Operations.Services.SchemaRegistry.Generated.Schema generated)
        {
            return new()
            {
                Description = generated.Description,
                DisplayName = generated.DisplayName,
                Format = toModel(generated.Format),
                Hash = generated.Hash,
                Name = generated.Name,
                Namespace = generated.Namespace,
                SchemaContent = generated.SchemaContent,
                SchemaType = toModel(generated.SchemaType),
                Tags = generated.Tags,
                Version = generated.Version,
            };
        }

        internal static SchemaRegistry.Format toModel(string generated)
        {
            if (generated.Equals("Delta/1.0"))
            {
                return SchemaRegistry.Format.Delta1;
            }
            else if (generated.Equals("JsonSchema/draft-07"))
            {
                return SchemaRegistry.Format.JsonSchemaDraft07;
            }

            throw new ArgumentException("Received unknown schema registry format: " + generated);
        }

        internal static SchemaRegistry.SchemaType toModel(Azure.Iot.Operations.Services.SchemaRegistry.Generated.SchemaType generated)
        {
            // This is the only value in the enum as of now
            if (generated == Generated.SchemaType.MessageSchema)
            {
                return SchemaRegistry.SchemaType.MessageSchema;
            }

            throw new ArgumentException("Received unknown schema type " + generated.ToString());
        }

        internal static Azure.Iot.Operations.Services.SchemaRegistry.Generated.SchemaType fromModel(SchemaRegistry.SchemaType model)
        {
            // This is the only value in the enum as of now
            if (model == SchemaRegistry.SchemaType.MessageSchema)
            {
                return Generated.SchemaType.MessageSchema;
            }

            throw new ArgumentException("Received unknown schema type " + model.ToString());
        }

        internal static string fromModel(SchemaRegistry.Format model)
        {
            if (model == SchemaRegistry.Format.Delta1)
            {
                return "Delta/1.0";
            }
            else if (model == SchemaRegistry.Format.JsonSchemaDraft07)
            {
                return "JsonSchema/draft-07";
            }

            throw new ArgumentException("Received unknown schema registry format: " + model);
        }
    }
}
