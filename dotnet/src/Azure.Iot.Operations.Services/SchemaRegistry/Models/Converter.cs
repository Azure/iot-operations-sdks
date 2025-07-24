// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.SchemaRegistry.Models
{
    /// <summary>
    /// Class for converting from generated types to wrapped types
    /// </summary>
    internal class Converter
    {
        internal static Models.SchemaRegistryErrorCode toModel(Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry.SchemaRegistryErrorCode generated)
        {
            return (Models.SchemaRegistryErrorCode)(int)generated;
        }


        internal static Models.SchemaRegistryErrorDetails toModel(Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry.SchemaRegistryErrorDetails generated)
        {
            return new()
            {
                Code = generated.Code,
                CorrelationId = generated.CorrelationId,
                Message = generated.Message,
            };
        }


        internal static Models.SchemaRegistryErrorException toModel(Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry.SchemaRegistryErrorException generated)
        {
            // This includes setting the inner exception as the generated exception
            return new(toModel(generated.SchemaRegistryError), generated)
            {
                SchemaRegistryError = toModel(generated.SchemaRegistryError),
            };
        }

        internal static Models.SchemaRegistryError toModel(Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry.SchemaRegistryError generated)
        {

            return new()
            {
                Code = toModel(generated.Code),
                Details = generated.Details != null ? toModel(generated.Details) : null,
                InnerError = generated.InnerError != null ? toModel(generated.InnerError) : null,
                Message = generated.Message,
                Target = generated.Target != null ? (Models.SchemaRegistryErrorTarget)(int) generated.Target : null,
            };
        }

    }
}
