namespace Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Akri.Dtdl.Codegen;
    using DTDLParser;
    using DTDLParser.Models;

    public static class SchemaExtractor
    {
        public static SchemaTypeInfo? GetSchemaTypeInfo(Dtmi interfaceId, DTSchemaInfo? schemaInfo)
        {
            if (schemaInfo == null)
            {
                return null;
            }

            string specializedSchemaName = NameFormatter.DtmiToSchemaName(schemaInfo.Id, interfaceId, schemaInfo.EntityKind.ToString());

            switch (schemaInfo.EntityKind)
            {
                case DTEntityKind.Array:
                    return new ArrayTypeInfo(specializedSchemaName, GetSchemaTypeInfo(interfaceId, ((DTArrayInfo)schemaInfo).ElementSchema)!);
                case DTEntityKind.Map:
                    return new MapTypeInfo(specializedSchemaName, GetSchemaTypeInfo(interfaceId, ((DTMapInfo)schemaInfo).MapValue.Schema)!);
                case DTEntityKind.Object:
                    return new ObjectTypeInfo(specializedSchemaName, ((DTObjectInfo)schemaInfo).Fields.ToDictionary(f => f.Name, f => GetSchemaTypeInfo(interfaceId, f.Schema)!));
                case DTEntityKind.Enum:
                    return new EnumTypeInfo(specializedSchemaName, ((DTEnumInfo)schemaInfo).EnumValues.Select(e => e.Name).ToList());
                case DTEntityKind.Boolean:
                case DTEntityKind.Date:
                case DTEntityKind.DateTime:
                case DTEntityKind.Double:
                case DTEntityKind.Duration:
                case DTEntityKind.Float:
                case DTEntityKind.Integer:
                case DTEntityKind.Long:
                case DTEntityKind.String:
                case DTEntityKind.Time:
                    return new PrimitiveTypeInfo(schemaInfo.EntityKind.ToString());
                default:
                    throw new Exception($"inappropriate schema type {schemaInfo.EntityKind}");
            }
        }

        public static SchemaTypeInfo? GetCmdReqTypeInfo(DTInterfaceInfo dtInterface, JsonElement cmdElt)
        {
            string cmdName = cmdElt.GetProperty(AnnexFileProperties.CommandName).GetString()!;
            string? reqSchemaClass = cmdElt.GetProperty(AnnexFileProperties.CmdRequestSchema).GetString();
            DTCommandPayloadInfo dtCmdReq = dtInterface.Commands[cmdName].Request;

            return reqSchemaClass != null ? new ObjectTypeInfo(reqSchemaClass, new Dictionary<string, SchemaTypeInfo> { { dtCmdReq.Name, GetSchemaTypeInfo(dtInterface.Id, dtCmdReq.Schema)! } }) : null;
        }

        public static SchemaTypeInfo? GetCmdRespTypeInfo(DTInterfaceInfo dtInterface, JsonElement cmdElt)
        {
            string cmdName = cmdElt.GetProperty(AnnexFileProperties.CommandName).GetString()!;
            string? respSchemaClass = cmdElt.GetProperty(AnnexFileProperties.CmdResponseSchema).GetString();
            DTCommandPayloadInfo dtCmdResp = dtInterface.Commands[cmdName].Response;

            return respSchemaClass != null ? new ObjectTypeInfo(respSchemaClass, new Dictionary<string, SchemaTypeInfo> { { dtCmdResp.Name, GetSchemaTypeInfo(dtInterface.Id, dtCmdResp.Schema)! } }) : null;
        }

        public static SchemaTypeInfo GetTelemTypeInfo(DTInterfaceInfo dtInterface, JsonElement telemElt)
        {
            string? telemName = telemElt.TryGetProperty(AnnexFileProperties.TelemName, out JsonElement nameElt) ? nameElt.GetString() : null;
            string schemaClass = telemElt.GetProperty(AnnexFileProperties.TelemSchema).GetString()!;

            if (telemName != null)
            {
                return new ObjectTypeInfo(schemaClass, new Dictionary<string, SchemaTypeInfo> { { telemName, GetSchemaTypeInfo(dtInterface.Id, dtInterface.Telemetries[telemName].Schema)! } });
            }
            else
            {
                return new ObjectTypeInfo(schemaClass, dtInterface.Telemetries.ToDictionary(t => t.Key, t => GetSchemaTypeInfo(dtInterface.Id, t.Value.Schema)!));
            }
        }

        public static List<EnumTypeInfo> GetEnumTypeInfos(IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict, Dtmi interfaceId)
        {
            List<EnumTypeInfo> enumSchemas = new();

            foreach (DTEntityInfo dtEntity in modelDict.Values)
            {
                if (dtEntity.EntityKind == DTEntityKind.Enum && dtEntity.DefinedIn == interfaceId)
                {
                    DTEnumInfo dtEnum = (DTEnumInfo)dtEntity;
                    string specializedSchemaName = NameFormatter.DtmiToSchemaName(dtEnum.Id, interfaceId, "Enum");
                    enumSchemas.Add(new EnumTypeInfo(specializedSchemaName, dtEnum.EnumValues.Select(e => e.Name).ToList()));
                }
            }

            return enumSchemas;
        }
    }
}
