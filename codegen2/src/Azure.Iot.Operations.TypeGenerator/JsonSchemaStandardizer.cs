// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Azure.Iot.Operations.CodeGeneration;

    internal class JsonSchemaStandardizer : ISchemaStandardizer
    {
        private static readonly Regex TitleRegex = new(@"^[A-Z][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly Regex EnumValueRegex = new(@"^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

        private readonly TypeNamer typeNamer;

        internal JsonSchemaStandardizer(TypeNamer typeNamer)
        {
            this.typeNamer = typeNamer;
        }

        public SerializationFormat SerializationFormat { get => SerializationFormat.Json; }

        public bool TryGetStandardizedSchemas(Dictionary<string, string> schemaTextsByName, ErrorLog errorLog, out List<SchemaType> schemaTypes)
        {
            Dictionary<string, SchemaRoot> schemaRootsByName = GetSchemaRootsFromSchemaTexts(schemaTextsByName, errorLog);

            Dictionary<CodeName, SchemaType> schemaTypeDict = new();
            bool hasError = false;

            foreach (KeyValuePair<string, SchemaRoot> namedSchemaRoot in schemaRootsByName)
            {
                if (!TryGetSchemaType(namedSchemaRoot.Key, null, namedSchemaRoot.Value.JsonTracker, false, schemaTypeDict, schemaRootsByName, namedSchemaRoot.Value.ErrorReporter, out _, out _, true))
                {
                    hasError = true;
                }

                foreach (string internalDefsKey in JsonSchemaValues.InternalDefsKeys)
                {
                    if (namedSchemaRoot.Value.JsonTracker.TryGetProperty(internalDefsKey, out JsonTracker defsTracker))
                    {
                        foreach (KeyValuePair<string, JsonTracker> defProp in defsTracker.EnumerateObject())
                        {
                            if (!TryGetSchemaType(namedSchemaRoot.Key, defProp.Key, defProp.Value, false, schemaTypeDict, schemaRootsByName, namedSchemaRoot.Value.ErrorReporter, out _, out _, false))
                            {
                                hasError = true;
                            }
                        }
                    }
                }
            }

            schemaTypes = schemaTypeDict.Values.ToList();
            return !hasError;
        }

        private Dictionary<string, SchemaRoot> GetSchemaRootsFromSchemaTexts(Dictionary<string, string> schemaTextsByName, ErrorLog errorLog)
        {
            Dictionary<string, SchemaRoot> schemaRootsByName = new();

            foreach (KeyValuePair<string, string> namedSchemaText in schemaTextsByName)
            {
                byte[] schemaBytes = Encoding.UTF8.GetBytes(namedSchemaText.Value);
                string schemaFilePath = Path.GetFullPath(namedSchemaText.Key);
                string schemaFolder = Path.GetDirectoryName(namedSchemaText.Value)!;
                ErrorReporter errorReporter = new ErrorReporter(errorLog, schemaFilePath, schemaBytes);
                Utf8JsonReader reader = new Utf8JsonReader(schemaBytes);
                reader.Read();
                schemaRootsByName[namedSchemaText.Key] = new SchemaRoot(JsonTracker.Deserialize(ref reader), schemaFilePath, schemaFolder, errorReporter);
            }

            return schemaRootsByName;
        }

        private bool TryGetSchemaType(
            string docName,
            string? defKey,
            JsonTracker schemaTracker,
            bool orNull,
            Dictionary<CodeName, SchemaType>? schemaTypes,
            Dictionary<string, SchemaRoot> schemaRootsByName,
            ErrorReporter? errorReporter,
            [NotNullWhen(true)] out SchemaType? schemaType,
            [NotNullWhen(true)] out string? jsonSchemaType,
            bool isTopLevel = false)
        {
            schemaType = null;
            jsonSchemaType = null;
            bool hasError = false;

            if (schemaTracker.ValueKind != JsonValueKind.Object)
            {
                errorReporter?.ReportError(ErrorCondition.JsonInvalid, "JSON Schema definition has non-object value", schemaTracker.TokenIndex);
                return false;
            }

            if (!TryGetNestedNullableJsonElement(ref schemaTracker, errorReporter, ref orNull))
            {
                return false;
            }

            if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyTitle, out JsonTracker titleTracker))
            {
                string? title = titleTracker.GetString();
                if (titleTracker.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(title))
                {
                    errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"JSON Schema '{JsonSchemaValues.PropertyTitle}' property has non-string or empty value", titleTracker.TokenIndex);
                    hasError = true;
                }
                else if (!this.typeNamer.SuppressTitles && !TitleRegex.IsMatch(title))
                {
                    errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"JSON Schema '{JsonSchemaValues.PropertyTitle}' property value \"{title}\" does not conform to codegen type naming rules -- it must start with an uppercase letter and contain only alphanumeric characters and underscores", titleTracker.TokenIndex);
                    hasError = true;
                }
            }
            else if (isTopLevel)
            {
                errorReporter?.ReportError(ErrorCondition.PropertyMissing, $"JSON Schema file missing top-level '{JsonSchemaValues.PropertyTitle}' property", schemaTracker.TokenIndex);
                hasError = true;
            }

            if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyDescription, out JsonTracker descTracker))
            {
                if (descTracker.ValueKind != JsonValueKind.String)
                {
                    errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"JSON Schema '{JsonSchemaValues.PropertyDescription}' property has non-string value", descTracker.TokenIndex);
                    hasError = true;
                }
            }

            if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyRef, out _))
            {
                return TryGetReferenceSchemaType(docName, defKey, schemaTracker, orNull, schemaTypes, schemaRootsByName, errorReporter, out schemaType, out jsonSchemaType) && !hasError;
            }

            if (!schemaTracker.TryGetProperty(JsonSchemaValues.PropertyType, out JsonTracker typeTracker))
            {
                errorReporter?.ReportError(ErrorCondition.PropertyMissing, $"JSON Schema definition has neither '{JsonSchemaValues.PropertyType}' nor '{JsonSchemaValues.PropertyRef}' property", schemaTracker.TokenIndex);
                return false;
            }

            if (typeTracker.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(typeTracker.GetString()))
            {
                errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"JSON Schema '{JsonSchemaValues.PropertyType}' property has non-string or empty value", typeTracker.TokenIndex);
                return false;
            }

            if (hasError)
            {
                return false;
            }

            jsonSchemaType = typeTracker.GetString();
            switch (jsonSchemaType)
            {
                case JsonSchemaValues.TypeObject:
                    return TryGetObjectSchemaType(docName, defKey, schemaTracker, orNull, schemaTypes, schemaRootsByName, errorReporter, out schemaType, isTopLevel);
                case JsonSchemaValues.TypeArray:
                    return TryGetArraySchemaType(docName, defKey, schemaTracker, orNull, schemaTypes, schemaRootsByName, errorReporter, out schemaType);
                case JsonSchemaValues.TypeString:
                    return TryGetStringSchemaType(docName, defKey, schemaTracker, orNull, schemaTypes, schemaRootsByName, errorReporter, out schemaType);
                case JsonSchemaValues.TypeInteger:
                    return TryGetIntegerSchemaType(schemaTracker, orNull, errorReporter, out schemaType);
                case JsonSchemaValues.TypeNumber:
                    return TryGetNumberSchemaType(schemaTracker, orNull, errorReporter, out schemaType);
                case JsonSchemaValues.TypeBoolean:
                    return TryGetBooleanSchemaType(schemaTracker, orNull, errorReporter, out schemaType);
                default:
                    errorReporter?.ReportError(ErrorCondition.PropertyUnsupportedValue, $"JSON Schema '{JsonSchemaValues.PropertyType}' property has unrecognized value \"{typeTracker.GetString()}\"", typeTracker.TokenIndex);
                    return false;
            }
        }

        private bool TryGetNestedNullableJsonElement(ref JsonTracker jsonTracker, ErrorReporter? errorReporter, ref bool orNull)
        {
            if (!jsonTracker.TryGetProperty(JsonSchemaValues.PropertyAnyOf, out JsonTracker anyOfTracker))
            {
                return true;
            }

            if (anyOfTracker.ValueKind != JsonValueKind.Array)
            {
                errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyAnyOf}' property has non-array value", anyOfTracker.TokenIndex);
                return false;
            }

            if (anyOfTracker.GetArrayLength() != 2)
            {
                errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyAnyOf}' property must have exactly two elements to represent a nullable type", anyOfTracker.TokenIndex);
                return false;
            }

            if (!TryDetermineNullType(anyOfTracker[0], "first", errorReporter, out bool firstIsNull) ||
                !TryDetermineNullType(anyOfTracker[1], "second", errorReporter, out bool secondIsNull))
            {
                return false;
            }

            if (firstIsNull && secondIsNull)
            {
                errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyAnyOf}' property has two elements that both have type '{JsonSchemaValues.TypeNull}'", anyOfTracker.TokenIndex);
                return false;
            }
            if (!firstIsNull && !secondIsNull)
            {
                errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyAnyOf}' property has two elements neither of which has type '{JsonSchemaValues.TypeNull}'", anyOfTracker.TokenIndex);
                return false;
            }

            jsonTracker = firstIsNull ? anyOfTracker[1] : anyOfTracker[0];
            orNull = true;
            return true;
        }

        private bool TryDetermineNullType(JsonTracker tracker, string ordinal, ErrorReporter? errorReporter, out bool isNull)
        {
            isNull = false;

            if (tracker.ValueKind != JsonValueKind.Object)
            {
                errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyAnyOf}' {ordinal} element is not a JSON object", tracker.TokenIndex);
                return false;
            }
            if (!tracker.TryGetProperty(JsonSchemaValues.PropertyType, out JsonTracker typeTracker))
            {
                errorReporter?.ReportError(ErrorCondition.PropertyMissing, $"JSON Schema '{JsonSchemaValues.PropertyAnyOf}' {ordinal} element missing '{JsonSchemaValues.PropertyType}' property", tracker.TokenIndex);
                return false;
            }
            if (typeTracker.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(typeTracker.GetString()))
            {
                errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"JSON Schema '{JsonSchemaValues.PropertyAnyOf}' {ordinal} element '{JsonSchemaValues.PropertyType}' property has non-string or empty value", typeTracker.TokenIndex);
                return false;
            }
            if (typeTracker.GetString() == JsonSchemaValues.TypeNull)
            {
                isNull = true;
                return true;
            }

            return true;
        }

        private bool TryGetReferenceSchemaType(
            string docName,
            string? defKey,
            JsonTracker schemaTracker,
            bool orNull,
            Dictionary<CodeName, SchemaType>? schemaTypes,
            Dictionary<string, SchemaRoot> schemaRootsByName,
            ErrorReporter? errorReporter,
            [NotNullWhen(true)] out SchemaType? schemaType,
            [NotNullWhen(true)] out string? jsonSchemaType)
        {
            schemaType = null;
            jsonSchemaType = null;
            bool hasError = false;

            JsonTracker referencingTracker = schemaTracker.GetProperty(JsonSchemaValues.PropertyRef);

            if (!TryGetReferenceInfo(docName, referencingTracker, schemaRootsByName, errorReporter, out string refName, out string? refKey, out JsonTracker refTracker))
            {
                hasError = true;
            }

            foreach (KeyValuePair<string, JsonTracker> prop in schemaTracker.EnumerateObject())
            {
                if (prop.Key != JsonSchemaValues.PropertySchema && 
                    prop.Key != JsonSchemaValues.PropertyRef &&
                    prop.Key != JsonSchemaValues.PropertyType &&
                    prop.Key != JsonSchemaValues.PropertyTitle &&
                    prop.Key != JsonSchemaValues.PropertyDescription)
                {
                    errorReporter?.ReportWarning($"JSON Schema element has '{JsonSchemaValues.PropertyRef}' property, so '{prop.Key}' property will be ignored", prop.Value.TokenIndex);
                }
            }

            if (hasError || !TryGetSchemaType(refName, refKey, refTracker, orNull, null, schemaRootsByName, null, out schemaType, out jsonSchemaType))
            {
                return false;
            }

            if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyType, out JsonTracker refTypeTracker))
            {
                if (refTypeTracker.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(refTypeTracker.GetString()))
                {
                    errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyType}' property has non-string or empty value", refTypeTracker.TokenIndex);
                    return false;
                }

                string referencedType = refTypeTracker.GetString();
                if (jsonSchemaType != referencedType)
                {
                    string refString = referencingTracker.GetString();
                    errorReporter?.ReportReferenceTypeError($"JSON Schema '{JsonSchemaValues.PropertyRef}' value", refString, referencingTracker.TokenIndex, referencedType, jsonSchemaType);
                    return false;
                }
            }

            if (schemaTypes != null && schemaTracker.TryGetProperty(JsonSchemaValues.PropertyTitle, out JsonTracker titleTracker) && schemaType is ReferenceType refType)
            {
                CodeName schemaName = new CodeName(this.typeNamer.GenerateTypeName(docName, null, titleTracker.GetString()));
                if (!refType.SchemaName.Equals(schemaName))
                {
                    errorReporter?.RegisterSchemaName(schemaName.AsGiven, schemaTracker.TokenIndex);
                    string? description = schemaTracker.TryGetProperty(JsonSchemaValues.PropertyDescription, out JsonTracker descTracker) ? descTracker.GetString() : null;
                    schemaTypes[schemaName] = new AliasType(schemaName, description, refType.SchemaName, orNull: false);
                }
            }

            return true;
        }

        private bool TryGetObjectSchemaType(
            string docName,
            string? defKey,
            JsonTracker schemaTracker,
            bool orNull,
            Dictionary<CodeName, SchemaType>? schemaTypes,
            Dictionary<string, SchemaRoot> schemaRootsByName,
            ErrorReporter? errorReporter,
            [NotNullWhen(true)] out SchemaType? schemaType,
            bool isTopLevel)
        {
            schemaType = null;

            if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyAdditionalProperties, out JsonTracker addlPropsTracker))
            {
                if (addlPropsTracker.ValueKind == JsonValueKind.Object)
                {
                    if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyProperties, out _))
                    {
                        errorReporter?.ReportError(ErrorCondition.ValuesInconsistent, $"JSON Schema element has both a '{JsonSchemaValues.PropertyProperties}' property and an object-valued '{JsonSchemaValues.PropertyAdditionalProperties}' property -- intended type is ambiguous between Object and Map", schemaTracker.TokenIndex);
                        return false;
                    }

                    foreach (KeyValuePair<string, JsonTracker> prop in schemaTracker.EnumerateObject())
                    {
                        if (prop.Key != JsonSchemaValues.PropertySchema &&
                            prop.Key != JsonSchemaValues.PropertyType &&
                            prop.Key != JsonSchemaValues.PropertyTitle &&
                            prop.Key != JsonSchemaValues.PropertyAdditionalProperties &&
                            prop.Key != JsonSchemaValues.PropertyDescription)
                        {
                            errorReporter?.ReportWarning($"JSON Schema element defines a Map type, so '{prop.Key}' property will be ignored", prop.Value.TokenIndex);
                        }
                    }

                    if (!TryGetSchemaType(
                        docName,
                        defKey,
                        addlPropsTracker,
                        orNull: false,
                        schemaTypes,
                        schemaRootsByName,
                        errorReporter,
                        out SchemaType? valueSchemaType,
                        out _))
                    {
                        return false;
                    }

                    schemaType = new MapType(valueSchemaType, orNull);
                    return true;
                }
                else if (addlPropsTracker.ValueKind != JsonValueKind.False)
                {
                    errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyAdditionalProperties}' property must have a value that is either a JSON object or a literal false", addlPropsTracker.TokenIndex);
                    return false;
                }
            }

            if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyProperties, out JsonTracker propertiesTracker))
            {
                bool hasError = false;

                if (propertiesTracker.ValueKind != JsonValueKind.Object)
                {
                    errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyProperties}' property has non-object value", propertiesTracker.TokenIndex);
                    return false;
                }

                HashSet<string> requiredFields = new();
                if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyRequired, out JsonTracker requiredTracker))
                {
                    if (requiredTracker.ValueKind != JsonValueKind.Array)
                    {
                        errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyRequired}' property has non-array value", requiredTracker.TokenIndex);
                        hasError = true;
                    }
                    else
                    {
                        foreach (JsonTracker reqTracker in requiredTracker.EnumerateArray())
                        {
                            string? reqName = reqTracker.GetString();
                            if (reqTracker.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(reqName))
                            {
                                errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyRequired}' element in array has non-string or empty value", reqTracker.TokenIndex);
                                hasError = true;
                            }
                            else if (!propertiesTracker.TryGetProperty(reqName, out _))
                            {
                                errorReporter?.ReportError(ErrorCondition.ItemNotFound, $"JSON Schema '{JsonSchemaValues.PropertyRequired}' element in array has value '{reqName}' that does not correspond to any property in '{JsonSchemaValues.PropertyProperties}' element", reqTracker.TokenIndex);
                                hasError = true;
                            }
                            else
                            {
                                requiredFields.Add(reqName!);
                            }
                        }
                    }
                }

                Dictionary<CodeName, ObjectType.FieldInfo> objectFields = new();
                if (schemaTypes != null)
                {
                    foreach (KeyValuePair<string, JsonTracker> objProp in propertiesTracker.EnumerateObject())
                    {
                        bool isRequired = requiredFields.Contains(objProp.Key);
                        if (TryGetSchemaType(
                            docName,
                            objProp.Key,
                            objProp.Value,
                            orNull: !isRequired,
                            schemaTypes,
                            schemaRootsByName,
                            errorReporter,
                            out SchemaType? fieldSchemaType,
                            out _))
                        {
                            string? fieldDesc = objProp.Value.TryGetProperty(JsonSchemaValues.PropertyDescription, out JsonTracker fieldDescTracker) ? fieldDescTracker.GetString() : null;
                            objectFields[new CodeName(objProp.Key)] = new ObjectType.FieldInfo(fieldSchemaType, isRequired, fieldDesc);
                        }
                        else
                        {
                            hasError = true;
                        }
                    }
                }

                if (!isTopLevel)
                {
                    foreach (string internalDefsKey in JsonSchemaValues.InternalDefsKeys)
                    {
                        if (schemaTracker.TryGetProperty(internalDefsKey, out JsonTracker internalDefsTracker))
                        {
                            errorReporter?.ReportWarning($"JSON Schema element is not a top-level Object type definition, so '{internalDefsKey}' property will be ignored", internalDefsTracker.TokenIndex);
                        }
                    }
                }

                foreach (KeyValuePair<string, JsonTracker> prop in schemaTracker.EnumerateObject())
                {
                    if (prop.Key != JsonSchemaValues.PropertySchema &&
                        prop.Key != JsonSchemaValues.PropertyType &&
                        prop.Key != JsonSchemaValues.PropertyTitle &&
                        prop.Key != JsonSchemaValues.PropertyProperties &&
                        prop.Key != JsonSchemaValues.PropertyAdditionalProperties &&
                        prop.Key != JsonSchemaValues.PropertyDescription &&
                        prop.Key != JsonSchemaValues.PropertyRequired &&
                        !JsonSchemaValues.InternalDefsKeys.Contains(prop.Key))
                    {
                        errorReporter?.ReportWarning($"JSON Schema element defines an Object type, so '{prop.Key}' property will be ignored", prop.Value.TokenIndex);
                    }
                }

                if (hasError)
                {
                    return false;
                }

                string? title = schemaTracker.TryGetProperty(JsonSchemaValues.PropertyTitle, out JsonTracker titleTracker) ? titleTracker.GetString() : null;
                CodeName schemaName = new CodeName(this.typeNamer.GenerateTypeName(docName, defKey, title));

                if (schemaTypes != null)
                {
                    errorReporter?.RegisterSchemaName(schemaName.AsGiven, schemaTracker.TokenIndex);
                    string? description = schemaTracker.TryGetProperty(JsonSchemaValues.PropertyDescription, out JsonTracker descTracker) ? descTracker.GetString() : null;
                    schemaTypes[schemaName] = new ObjectType(
                        schemaName,
                        description,
                        objectFields,
                        orNull: false);
                }

                schemaType = new ReferenceType(schemaName, isNullable: true, orNull: orNull);
                return true;
            }

            errorReporter?.ReportError(ErrorCondition.PropertyMissing, $"JSON Schema element has neither a '{JsonSchemaValues.PropertyProperties}' property nor an object-valued '{JsonSchemaValues.PropertyAdditionalProperties}' property", schemaTracker.TokenIndex);
            return false;
        }

        private bool TryGetArraySchemaType(
            string docName,
            string? defKey,
            JsonTracker schemaTracker,
            bool orNull,
            Dictionary<CodeName, SchemaType>? schemaTypes,
            Dictionary<string, SchemaRoot> schemaRootsByName,
            ErrorReporter? errorReporter,
            [NotNullWhen(true)] out SchemaType? schemaType)
        {
            schemaType = null;
            bool hasError = false;

            if (!schemaTracker.TryGetProperty(JsonSchemaValues.PropertyItems, out JsonTracker itemsTracker))
            {
                errorReporter?.ReportError(ErrorCondition.PropertyMissing, $"JSON Schema element has type '{JsonSchemaValues.TypeArray}' but no '{JsonSchemaValues.PropertyItems}' property", schemaTracker.TokenIndex);
                hasError = true;
            }
            else if (itemsTracker.ValueKind != JsonValueKind.Object)
            {
                errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyItems}' property has non-object value", itemsTracker.TokenIndex);
                hasError = true;
            }

            foreach (KeyValuePair<string, JsonTracker> prop in schemaTracker.EnumerateObject())
            {
                if (prop.Key != JsonSchemaValues.PropertySchema &&
                    prop.Key != JsonSchemaValues.PropertyType &&
                    prop.Key != JsonSchemaValues.PropertyTitle &&
                    prop.Key != JsonSchemaValues.PropertyItems &&
                    prop.Key != JsonSchemaValues.PropertyDescription)
                {
                    errorReporter?.ReportWarning($"JSON Schema element defines an Array type, so '{prop.Key}' property will be ignored", prop.Value.TokenIndex);
                }
            }

            if (hasError || !TryGetSchemaType(
                docName,
                defKey,
                itemsTracker,
                orNull: false,
                schemaTypes,
                schemaRootsByName,
                errorReporter,
                out SchemaType? itemSchemaType,
                out _))
            {
                return false;
            }

            schemaType = new ArrayType(itemSchemaType, orNull);
            return true;
        }

        private bool TryGetStringSchemaType(
            string docName,
            string? defKey,
            JsonTracker schemaTracker,
            bool orNull,
            Dictionary<CodeName, SchemaType>? schemaTypes,
            Dictionary<string, SchemaRoot> schemaRootsByName,
            ErrorReporter? errorReporter,
            [NotNullWhen(true)] out SchemaType? schemaType)
        {
            schemaType = null;
            int modifierCount = 0;
            bool hasError = false;

            if (!schemaTracker.TryGetProperty(JsonSchemaValues.PropertyEnum, out JsonTracker enumTracker))
            {
                if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyFormat, out JsonTracker formatTracker))
                {
                    modifierCount++;
                    if (formatTracker.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(formatTracker.GetString()))
                    {
                        errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyFormat}' property has non-string or empty value", formatTracker.TokenIndex);
                        hasError = true;
                    }
                }
                if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyContentEncoding, out JsonTracker encodingTracker))
                {
                    modifierCount++;
                    if (encodingTracker.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(encodingTracker.GetString()))
                    {
                        errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyContentEncoding}' property has non-string or empty value", encodingTracker.TokenIndex);
                        hasError = true;
                    }
                }
                if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyPattern, out JsonTracker patternTracker))
                {
                    modifierCount++;
                    if (patternTracker.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(patternTracker.GetString()))
                    {
                        errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyPattern}' property has non-string or empty value", patternTracker.TokenIndex);
                        hasError = true;
                    }
                }

                if (modifierCount > 1)
                {
                    errorReporter?.ReportError(ErrorCondition.ValuesInconsistent, $"JSON Schema '{JsonSchemaValues.TypeString}' type can have at most one of '{JsonSchemaValues.PropertyFormat}', '{JsonSchemaValues.PropertyContentEncoding}', or '{JsonSchemaValues.PropertyPattern}' properties", schemaTracker.TokenIndex);
                    hasError = true;
                }

                foreach (KeyValuePair<string, JsonTracker> prop in schemaTracker.EnumerateObject())
                {
                    if (prop.Key != JsonSchemaValues.PropertySchema &&
                        prop.Key != JsonSchemaValues.PropertyType &&
                        prop.Key != JsonSchemaValues.PropertyFormat &&
                        prop.Key != JsonSchemaValues.PropertyContentEncoding &&
                        prop.Key != JsonSchemaValues.PropertyPattern &&
                        prop.Key != JsonSchemaValues.PropertyDescription)
                    {
                        errorReporter?.ReportWarning($"JSON Schema element defines a String type, so '{prop.Key}' property will be ignored", prop.Value.TokenIndex);
                    }
                }

                if (hasError)
                {
                    return false;
                }

                if (formatTracker.ValueKind == JsonValueKind.String)
                {
                    switch (formatTracker.GetString()!)
                    {
                        case JsonSchemaValues.FormatDate:
                            schemaType = new DateType(orNull);
                            return true;
                        case JsonSchemaValues.FormatDateTime:
                            schemaType = new DateTimeType(orNull);
                            return true;
                        case JsonSchemaValues.FormatTime:
                            schemaType = new TimeType(orNull);
                            return true;
                        case JsonSchemaValues.FormatDuration:
                            schemaType = new DurationType(orNull);
                            return true;
                        case JsonSchemaValues.FormatUuid:
                            schemaType = new UuidType(orNull);
                            return true;
                        default:
                            errorReporter?.ReportError(ErrorCondition.PropertyUnsupportedValue, $"JSON Schema '{JsonSchemaValues.PropertyFormat}' property has unrecognized value \"{formatTracker.GetString()}\"", formatTracker.TokenIndex);
                            return false;
                    }
                }

                if (encodingTracker.ValueKind == JsonValueKind.String)
                {
                    switch (encodingTracker.GetString()!)
                    {
                        case JsonSchemaValues.ContentEncodingBase64:
                            schemaType = new BytesType(orNull);
                            return true;
                        default:
                            errorReporter?.ReportError(ErrorCondition.PropertyUnsupportedValue, $"JSON Schema '{JsonSchemaValues.PropertyContentEncoding}' property has unrecognized value \"{encodingTracker.GetString()}\"", encodingTracker.TokenIndex);
                            return false;
                    }
                }

                if (patternTracker.ValueKind == JsonValueKind.String)
                {
                    switch (patternTracker.GetString())
                    {
                        case JsonSchemaValues.PatternDecimal:
                            schemaType = new DecimalType(orNull);
                            return true;
                        default:
                            errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"JSON Schema '{JsonSchemaValues.PropertyPattern}' property has unprocessable value \"{patternTracker.GetString()}\"", patternTracker.TokenIndex);
                            return false;
                    }
                }

                schemaType = new StringType(orNull);
                return true;
            }

            List<CodeName> enumValues = new();
            if (enumTracker.ValueKind != JsonValueKind.Array)
            {
                errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyEnum}' property has non-array value", enumTracker.TokenIndex);
                hasError = true;
            }
            else
            {
                foreach (JsonTracker valueTracker in enumTracker.EnumerateArray())
                {
                    if (valueTracker.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(valueTracker.GetString()))
                    {
                        errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyEnum}' element in array has non-string or empty value", valueTracker.TokenIndex);
                        hasError = true;
                    }
                    else if (!EnumValueRegex.IsMatch(valueTracker.GetString()!))
                    {
                        errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"JSON Schema '{JsonSchemaValues.PropertyEnum}' element value \"{valueTracker.GetString()}\"must start with a letter and contain only alphanumerics and underscores", valueTracker.TokenIndex);
                        hasError = true;
                    }
                    else
                    {
                        enumValues.Add(new CodeName(valueTracker.GetString()!));
                    }
                }
            }

            foreach (KeyValuePair<string, JsonTracker> prop in schemaTracker.EnumerateObject())
            {
                if (prop.Key != JsonSchemaValues.PropertySchema &&
                    prop.Key != JsonSchemaValues.PropertyType &&
                    prop.Key != JsonSchemaValues.PropertyTitle &&
                    prop.Key != JsonSchemaValues.PropertyEnum &&
                    prop.Key != JsonSchemaValues.PropertyDescription)
                {
                    errorReporter?.ReportWarning($"JSON Schema element defines an enumerated String type, so '{prop.Key}' property will be ignored", prop.Value.TokenIndex);
                }
            }

            if (hasError)
            {
                return false;
            }

            string? title = schemaTracker.TryGetProperty(JsonSchemaValues.PropertyTitle, out JsonTracker titleTracker) ? titleTracker.GetString() : null;
            CodeName schemaName = new CodeName(this.typeNamer.GenerateTypeName(docName, defKey, title));

            if (schemaTypes != null)
            {
                errorReporter?.RegisterSchemaName(schemaName.AsGiven, schemaTracker.TokenIndex);
                string? description = schemaTracker.TryGetProperty(JsonSchemaValues.PropertyDescription, out JsonTracker descTracker) ? descTracker.GetString() : null;
                schemaTypes[schemaName] = new EnumType(
                    schemaName,
                    description,
                    enumValues.ToArray(),
                    orNull: false);
            }

            schemaType = new ReferenceType(schemaName, isNullable: false, orNull: orNull);
            return true;
        }

        private bool TryGetIntegerSchemaType(JsonTracker schemaTracker, bool orNull, ErrorReporter? errorReporter, [NotNullWhen(true)] out SchemaType? schemaType)
        {
            schemaType = null;
            bool hasError = false;
            long minimum = 0;
            ulong maximum = ulong.MaxValue;

            if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyMaximum, out JsonTracker maxTracker))
            {
                if (maxTracker.ValueKind != JsonValueKind.Number)
                {
                    errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyMaximum}' property has non-numeric value", maxTracker.TokenIndex);
                    hasError = true;
                }
                else if (!double.IsInteger(maxTracker.GetDouble()))
                {
                    errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"JSON Schema '{JsonSchemaValues.PropertyMaximum}' property has non-integer numeric value", maxTracker.TokenIndex);
                    hasError = true;
                }
                else if (maxTracker.GetDouble() < 0)
                {
                    errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"JSON Schema '{JsonSchemaValues.PropertyMaximum}' property has negative value", maxTracker.TokenIndex);
                    hasError = true;
                }
                else
                {
                    maximum = maxTracker.GetUInt64();
                }
            }

            if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyMinimum, out JsonTracker minTracker))
            {
                if (minTracker.ValueKind != JsonValueKind.Number)
                {
                    errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyMinimum}' property has non-numeric value", minTracker.TokenIndex);
                    hasError = true;
                }
                else if (!double.IsInteger(minTracker.GetDouble()))
                {
                    errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"JSON Schema '{JsonSchemaValues.PropertyMinimum}' property has non-integer numeric value", minTracker.TokenIndex);
                    hasError = true;
                }
                else if (minTracker.GetDouble() > 0)
                {
                    errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"JSON Schema '{JsonSchemaValues.PropertyMinimum}' property has positive value", minTracker.TokenIndex);
                    hasError = true;
                }
                else
                {
                    minimum = minTracker.GetInt64();
                }
            }

            foreach (KeyValuePair<string, JsonTracker> prop in schemaTracker.EnumerateObject())
            {
                if (prop.Key != JsonSchemaValues.PropertySchema &&
                    prop.Key != JsonSchemaValues.PropertyType &&
                    prop.Key != JsonSchemaValues.PropertyMaximum &&
                    prop.Key != JsonSchemaValues.PropertyMinimum &&
                    prop.Key != JsonSchemaValues.PropertyDescription)
                {
                    errorReporter?.ReportWarning($"JSON Schema element defines an Integer type, so '{prop.Key}' property will be ignored", prop.Value.TokenIndex);
                }
            }

            if (hasError)
            {
                return false;
            }

            schemaType = (minimum, maximum) switch
            {
                ( >= (long)byte.MinValue, <= (ulong)byte.MaxValue) => new UnsignedByteType(orNull),
                ( >= (long)ushort.MinValue, <= (ulong)ushort.MaxValue) => new UnsignedShortType(orNull),
                ( >= (long)uint.MinValue, <= (ulong)uint.MaxValue) => new UnsignedIntegerType(orNull),
                ( >= (long)ulong.MinValue, <= (ulong)ulong.MaxValue) => new UnsignedLongType(orNull),
                ( >= (long)sbyte.MinValue, <= (ulong)sbyte.MaxValue) => new ByteType(orNull),
                ( >= (long)short.MinValue, <= (ulong)short.MaxValue) => new ShortType(orNull),
                ( >= (long)int.MinValue, <= (ulong)int.MaxValue) => new IntegerType(orNull),
                ( >= (long)long.MinValue, <= (ulong)long.MaxValue) => new LongType(orNull),
                _ => new UnsignedLongType(orNull),
            };

            return true;
        }

        private bool TryGetNumberSchemaType(JsonTracker schemaTracker, bool orNull, ErrorReporter? errorReporter, [NotNullWhen(true)] out SchemaType? schemaType)
        {
            schemaType = null;
            bool hasError = false;
            bool isDouble = true;

            if (schemaTracker.TryGetProperty(JsonSchemaValues.PropertyFormat, out JsonTracker formatTracker))
            {
                if (formatTracker.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(formatTracker.GetString()))
                {
                    errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyFormat}' property has non-string or empty value", formatTracker.TokenIndex);
                    hasError = true;
                }
                else
                {
                    switch (formatTracker.GetString()!)
                    {
                        case JsonSchemaValues.FormatFloat:
                            isDouble = false;
                            break;
                        case JsonSchemaValues.FormatDouble:
                            isDouble = true;
                            break;
                        default:
                            errorReporter?.ReportError(ErrorCondition.PropertyUnsupportedValue, $"JSON Schema '{JsonSchemaValues.PropertyFormat}' property has unrecognized value -- must be either '{JsonSchemaValues.FormatFloat}' or '{JsonSchemaValues.FormatDouble}'", formatTracker.TokenIndex);
                            hasError = true;
                            break;
                    }
                }
            }

            foreach (KeyValuePair<string, JsonTracker> prop in schemaTracker.EnumerateObject())
            {
                if (prop.Key != JsonSchemaValues.PropertySchema &&
                    prop.Key != JsonSchemaValues.PropertyType &&
                    prop.Key != JsonSchemaValues.PropertyFormat &&
                    prop.Key != JsonSchemaValues.PropertyDescription)
                {
                    errorReporter?.ReportWarning($"JSON Schema element defines a Number type, so '{prop.Key}' property will be ignored", prop.Value.TokenIndex);
                }
            }

            if (hasError)
            {
                return false;
            }

            schemaType = isDouble ? new DoubleType(orNull) : new FloatType(orNull);
            return true;
        }

        private bool TryGetBooleanSchemaType(JsonTracker schemaTracker, bool orNull, ErrorReporter? errorReporter, [NotNullWhen(true)] out SchemaType? schemaType)
        {
            foreach (KeyValuePair<string, JsonTracker> prop in schemaTracker.EnumerateObject())
            {
                if (prop.Key != JsonSchemaValues.PropertySchema &&
                    prop.Key != JsonSchemaValues.PropertyType &&
                    prop.Key != JsonSchemaValues.PropertyDescription)
                {
                    errorReporter?.ReportWarning($"JSON Schema element defines a Boolean type, so '{prop.Key}' property will be ignored", prop.Value.TokenIndex);
                }
            }

            schemaType = new BooleanType(orNull);
            return true;
        }

        private bool TryGetReferenceInfo(
            string docName,
            JsonTracker referencingTracker,
            Dictionary<string, SchemaRoot> schemaRootsByName,
            ErrorReporter? errorReporter,
            out string referencedName,
            out string? referencedKey,
            out JsonTracker referencedTracker)
        {
            referencedName = string.Empty;
            referencedKey = null;
            referencedTracker = new JsonTracker();

            if (referencingTracker.ValueKind != JsonValueKind.String)
            {
                errorReporter?.ReportError(ErrorCondition.JsonInvalid, $"JSON Schema '{JsonSchemaValues.PropertyRef}' property has non-string value", referencingTracker.TokenIndex);
                return false;
            }

            if (string.IsNullOrEmpty(referencingTracker.GetString()))
            {
                errorReporter?.ReportError(ErrorCondition.PropertyEmpty, $"JSON Schema '{JsonSchemaValues.PropertyRef}' property has empty string value", referencingTracker.TokenIndex);
                return false;
            }

            string refString = referencingTracker.GetString();
            string unescapedString = Uri.UnescapeDataString(refString);
            int fragIx = unescapedString.IndexOf('#');

            string baseRef = fragIx switch
            {
                < 0 => unescapedString,
                > 0 => unescapedString.Substring(0, fragIx),
                0 => docName,
            };
            string fragment = fragIx < 0 ? string.Empty : unescapedString.Substring(fragIx + 2);

            referencedName = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(docName)!, baseRef)).Replace('\\', '/');
            if (!schemaRootsByName.TryGetValue(referencedName, out SchemaRoot? schemaRoot) || schemaRoot == null)
            {
                errorReporter?.ReportReferenceError($"JSON Schema '{JsonSchemaValues.PropertyRef}' value", $"no file provided with name {referencedName}", refString, referencingTracker.TokenIndex);
                return false;
            }

            int sepIx = fragment.IndexOf('/');
            string? refCollection = sepIx > 0 ? fragment.Substring(0, sepIx) : null;
            referencedKey = sepIx > 0 ? fragment.Substring(sepIx + 1) : null;

            if (referencedKey != null)
            {
                if (!schemaRoot.JsonTracker.TryGetProperty(refCollection!, out JsonTracker collectionTracker))
                {
                    errorReporter?.ReportReferenceError($"JSON Schema '{JsonSchemaValues.PropertyRef}' value", $"no root '{refCollection}' property found in {referencedName}", refString, referencingTracker.TokenIndex);
                    return false;
                }

                if (!collectionTracker.TryGetProperty(referencedKey, out referencedTracker))
                {
                    errorReporter?.ReportReferenceError($"JSON Schema '{JsonSchemaValues.PropertyRef}' value", $"no '{referencedKey}' property found under root '{refCollection}' property in {referencedName}", refString, referencingTracker.TokenIndex);
                    return false;
                }
            }
            else
            {
                referencedTracker = schemaRoot.JsonTracker;
            }

            return true;
        }
    }
}
