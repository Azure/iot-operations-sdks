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

    }
}
