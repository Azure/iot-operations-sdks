namespace Azure.Iot.Operations.CodeGeneration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    public class ThingValidator
    {
        private const string TdContextUri = "https://www.w3.org/2022/wot/td/v1.1";
        private const string AioContextUriBase = "http://azure.com/DigitalTwins/dtmi#";
        private const string AioContextPrefix = "dtv";

        private const string LinkRelSchemaNamer = "service-desc";

        private const string Iso8601DurationExample = "P3Y6M4DT12H30M5S";
        private const string DecimalExample = "1234567890.0987654321";
        private const string AnArbitraryString = "Pretty12345Tricky67890";

        private static readonly Regex TitleRegex = new(@"^[A-Z][A-Za-z0-9]*$", RegexOptions.Compiled);
        private static readonly Regex RefCharRegex = new(@"^(?:[!#$&-;=?-\[\]_a-z~]|\%[0-9a-fA-F]{2})+$", RegexOptions.Compiled);
        private static readonly Regex EnumValueRegex = new(@"^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

        private ErrorReporter errorReporter;

        public ThingValidator(ErrorReporter errorReporter)
        {
            this.errorReporter = errorReporter;
        }

        public bool TryValidateThng(TDThing thing, HashSet<SerializationFormat> serializationFormats)
        {
            bool hasError = false;

            if (!TryValidateContext(thing.Context))
            {
                hasError = true;
            }

            if (!TryValidateType(thing.Type))
            {
                hasError = true;
            }

            if (!TryValidateTitle(thing.Title))
            {
                hasError = true;
            }

            if (!TryValidateLinks(thing.Links))
            {
                hasError = true;
            }

            if (!TryValidateSchemaDefinitions(thing.SchemaDefinitions))
            {
                hasError = true;
            }

            if (!TryValidateRootForms(thing.Forms, thing.SchemaDefinitions, serializationFormats))
            {
                hasError = true;
            }

            if (!TryValidateActions(thing.Actions, thing.SchemaDefinitions, serializationFormats))
            {
                hasError = true;
            }

            if (!TryValidateProperties(thing.Properties, thing.SchemaDefinitions, serializationFormats))
            {
                hasError = true;
            }

            if (!TryValidateEvents(thing.Events, thing.SchemaDefinitions, serializationFormats))
            {
                hasError = true;
            }

            if (hasError)
            {
                return false;
            }

            if (!TryValidateCrossFormConsistency(thing.Forms, thing.Actions))
            {
                hasError = true;
            }

            if (!TryValidateCrossFormConsistency(thing.Forms, thing.Properties))
            {
                hasError = true;
            }

            if (!TryValidateCrossFormConsistency(thing.Forms, thing.Events))
            {
                hasError = true;
            }

            HashSet<string> supportedProperties = new()
            {
                TDThing.ContextName,
                TDThing.TypeName,
                TDThing.TitleName,
                TDThing.LinksName,
                TDThing.SchemaDefinitionsName,
                TDThing.FormsName,
                TDThing.ActionsName,
                TDThing.PropertiesName,
                TDThing.EventsName,
            };
            if (!TryValidateResidualProperties(thing.PropertyNames, supportedProperties, null, "an AIO Thing Model"))
            {
                hasError = true;
            }

            CheckSchemaDefinitionsCoverage(thing.SchemaDefinitions, thing.Actions, thing.Properties);

            if ((thing.Actions?.Entries?.Count ?? 0) == 0 && (thing.Properties?.Entries?.Count ?? 0) == 0 && (thing.Events?.Entries?.Count ?? 0) == 0)
            {
                errorReporter.ReportWarning("Thing Description has no actions, properties, or events defined.", -1);
            }

            return !hasError;
        }

        private void CheckSchemaDefinitionsCoverage(MapTracker<TDDataSchema>? schemaDefinitions, MapTracker<TDAction>? actions, MapTracker<TDProperty>? properties)
        {
            if (schemaDefinitions?.Entries == null)
            {
                return;
            }

            HashSet<string> unreferencedSchemaKeys = new(schemaDefinitions.Entries.Where(d => d.Value.Value.Const == null).Select(d => d.Key));

            if (actions?.Entries != null)
            {
                foreach (ValueTracker<TDAction> action in actions.Entries.Values)
                {
                    foreach (ValueTracker<TDForm> form in action.Value.Forms?.Elements ?? new())
                    {
                        foreach (ValueTracker<TDSchemaReference> schemaReference in form.Value.AdditionalResponses?.Elements ?? new())
                        {
                            if (schemaReference.Value.Schema?.Value != null)
                            {
                                unreferencedSchemaKeys.Remove(schemaReference.Value.Schema.Value.Value);
                            }
                        }

                        foreach (ValueTracker<TDSchemaReference> schemaReference in form.Value.HeaderInfo?.Elements ?? new())
                        {
                            if (schemaReference.Value.Schema?.Value != null)
                            {
                                unreferencedSchemaKeys.Remove(schemaReference.Value.Schema.Value.Value);
                            }
                        }

                        if (form.Value.HeaderCode?.Value != null)
                        {
                            unreferencedSchemaKeys.Remove(form.Value.HeaderCode.Value.Value);
                        }
                    }
                }
            }

            if (properties?.Entries != null)
            {
                foreach (ValueTracker<TDProperty> property in properties.Entries.Values)
                {
                    foreach (ValueTracker<TDForm> form in property.Value.Forms?.Elements ?? new())
                    {
                        foreach (ValueTracker<TDSchemaReference> schemaReference in form.Value.AdditionalResponses?.Elements ?? new())
                        {
                            if (schemaReference.Value.Schema?.Value != null)
                            {
                                unreferencedSchemaKeys.Remove(schemaReference.Value.Schema.Value.Value);
                            }
                        }
                    }
                }
            }

            foreach (string unreferencedSchemaKey in unreferencedSchemaKeys)
            {
                errorReporter.ReportWarning($"'{TDThing.SchemaDefinitionsName}' key '{unreferencedSchemaKey}' has a value that is neither a constant declaration nor a type that is referenced by any action or property; definition will be ignored.", schemaDefinitions.Entries[unreferencedSchemaKey].TokenIndex);
            }
        }

        private bool TryValidateRootForms(ArrayTracker<TDForm>? forms, MapTracker<TDDataSchema>? schemaDefinitions, HashSet<SerializationFormat> serializationFormats)
        {
            if (!TryValidateForms(forms, FormsKind.Root, schemaDefinitions, out ValueTracker<StringHolder>? contentType))
            {
                return false;
            }

            if (contentType != null)
            {
                serializationFormats.Add(ThingSupport.ContentTypeToFormat(contentType.Value.Value));
            }

            List<ValueTracker<StringHolder>> aggregateOps = forms?.Elements?.SelectMany(form => form.Value.Op?.Elements ?? new()).ToList() ?? new();
            ValueTracker<StringHolder>? writeMultiOp = aggregateOps.FirstOrDefault(op => op.Value.Value == TDValues.OpWriteMultProps);
            ValueTracker<StringHolder>? readAllOp = aggregateOps.FirstOrDefault(op => op.Value.Value == TDValues.OpReadAllProps);
            if (writeMultiOp != null && readAllOp == null)
            {
                errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"'{TDThing.FormsName}' array contains '{TDForm.OpName}' property with value '{TDValues.OpWriteMultProps}' but no '{TDForm.OpName}' property with value '{TDValues.OpReadAllProps}'.", writeMultiOp.TokenIndex, forms?.TokenIndex ?? -1);
                return false;
            }

            return true;
        }

        private bool TryValidateCrossFormConsistency(ArrayTracker<TDForm>? rootForms, MapTracker<TDAction>? actions)
        {
            bool hasError = false;

            if (actions?.Entries != null)
            {
                foreach (KeyValuePair<string, ValueTracker<TDAction>> action in actions.Entries)
                {
                    if (!(action.Value.Value.Forms?.Elements?.Any(f => f.Value.Topic != null) ?? false))
                    {
                        errorReporter.ReportError(ErrorCondition.Unusable, $"Action '{action.Key}' has no '{TDAction.FormsName}' element with a '{TDForm.TopicName}' property, so it cannot be invoked.", action.Value.TokenIndex);
                        hasError = true;
                    }
                }
            }

            return !hasError;
        }

        private bool TryValidateCrossFormConsistency(ArrayTracker<TDForm>? rootForms, MapTracker<TDProperty>? properties)
        {
            bool hasError = false;

            ValueTracker<TDForm>? readAllForm = rootForms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements != null && f.Value.Op.Elements.Any(op => op.Value.Value == TDValues.OpReadAllProps));
            ValueTracker<TDForm>? writeMultiForm = rootForms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements != null && f.Value.Op.Elements.Any(op => op.Value.Value == TDValues.OpWriteMultProps));

            bool aggregateReadHasAdditionalResponses = (readAllForm?.Value.AdditionalResponses?.Elements?.Count ?? 0) > 0;
            bool aggregateWriteHasAdditionalResponses = (writeMultiForm?.Value.AdditionalResponses?.Elements?.Count ?? 0) > 0;

            if (readAllForm != null)
            {
                if (properties?.Entries == null || properties.Entries.Count == 0)
                {
                    errorReporter.ReportError(ErrorCondition.Unusable, $"Root-level form has '{TDForm.OpName}' property with value '{TDValues.OpReadAllProps}' to read the aggregation of all properties, but Thing Description has no properties defined.",
                        readAllForm.Value.Op!.Elements!.First(op => op.Value.Value == TDValues.OpReadAllProps).TokenIndex,
                        properties?.TokenIndex ?? -1);
                    hasError = true;
                }
                else if (aggregateReadHasAdditionalResponses && !properties.Entries.Any(p => p.Value.Value.Forms?.Elements?.Any(f => (f.Value.Op?.Elements?.Any(op => op.Value.Value == TDValues.OpReadProp) ?? true) && (f.Value.AdditionalResponses?.Elements?.Count ?? 0) > 0) ?? false))
                {
                    errorReporter.ReportWarning($"Root-level form has '{TDForm.OpName}' value of '{TDValues.OpReadAllProps}' and an '{TDForm.AdditionalResponsesName}' value; however, no readable '{TDThing.PropertiesName}' element has a form with an '{TDForm.AdditionalResponsesName}' value to aggregate.",
                        readAllForm.TokenIndex,
                        properties?.TokenIndex ?? -1);
                }
            }

            if (writeMultiForm != null)
            {
                if (properties?.Entries == null || properties.Entries.Count(p => p.Value.Value.ReadOnly?.Value.Value != true && (p.Value.Value.Forms?.Elements?.Any(f => f.Value.Op?.Elements?.Any(op => op.Value.Value == TDValues.OpWriteProp) ?? true) ?? true)) == 0)
                {
                    errorReporter.ReportError(ErrorCondition.Unusable, $"Root-level form has '{TDForm.OpName}' property with value '{TDValues.OpWriteMultProps}' to write a selected aggregation of writable properties, but Thing Description has no writable properties.",
                        writeMultiForm.Value.Op!.Elements!.First(op => op.Value.Value == TDValues.OpWriteMultProps).TokenIndex,
                        properties?.TokenIndex ?? -1);
                    hasError = true;
                }
                else if (aggregateWriteHasAdditionalResponses && !properties.Entries.Any(p => p.Value.Value.ReadOnly?.Value.Value != true && (p.Value.Value.Forms?.Elements?.Any(f => (f.Value.Op?.Elements?.Any(op => op.Value.Value == TDValues.OpWriteProp) ?? true) && (f.Value.AdditionalResponses?.Elements?.Count ?? 0) > 0) ?? false)))
                {
                    errorReporter.ReportWarning($"Root-level form has '{TDForm.OpName}' value of '{TDValues.OpWriteMultProps}' and an '{TDForm.AdditionalResponsesName}' value; however, no writable '{TDThing.PropertiesName}' element has a form with an '{TDForm.AdditionalResponsesName}' value to aggregate.",
                        writeMultiForm.TokenIndex,
                        properties?.TokenIndex ?? -1);
                }
            }

            if (properties?.Entries != null)
            {
                foreach (KeyValuePair<string, ValueTracker<TDProperty>> prop in properties.Entries)
                {
                    if (prop.Value.Value.Forms?.Elements == null)
                    {
                        if (readAllForm == null)
                        {
                            errorReporter.ReportError(ErrorCondition.Unusable, $"Property '{prop.Key}' has no '{TDProperty.FormsName}' property, so it cannot be read individually; however, there is no root-level form with an '{TDForm.OpName}' property that has value '{TDValues.OpReadAllProps}', so this property also cannot be read in aggregate.",
                                prop.Value.TokenIndex,
                                rootForms?.TokenIndex ?? -1);
                            hasError = true;
                        }
                    }
                    else
                    {
                        foreach (ValueTracker<TDForm> form in prop.Value.Value.Forms.Elements)
                        {
                            bool propFormHasAdditionalResponses = (form.Value.AdditionalResponses?.Elements?.Count ?? 0) > 0;

                            if (form.Value.Topic == null)
                            {
                                if (form.Value.Op == null)
                                {
                                    if (readAllForm == null)
                                    {
                                        errorReporter.ReportError(ErrorCondition.Unusable, $"Property '{prop.Key}' has '{TDProperty.FormsName}' element with no '{TDForm.TopicName}' property, so it cannot be read individually; however, there is no root-level form with an '{TDForm.OpName}' property that has value '{TDValues.OpReadAllProps}', so this property also cannot be read in aggregate.",
                                            form.TokenIndex,
                                            rootForms?.TokenIndex ?? -1);
                                        hasError = true;
                                    }
                                    else if (propFormHasAdditionalResponses && !aggregateReadHasAdditionalResponses && !aggregateWriteHasAdditionalResponses)
                                    {
                                        errorReporter.ReportError(ErrorCondition.Unusable, $"Property '{prop.Key}' has '{TDProperty.FormsName}' element with no '{TDForm.TopicName}' property, so its '{TDForm.AdditionalResponsesName}' value cannot be returned on an individual read or write, nor can it be returned on an aggregate read or write because no root-level form with '{TDForm.OpName}' value of '{TDValues.OpReadAllProps}' or '{TDValues.OpWriteMultProps}' has an '{TDForm.AdditionalResponsesName}' value.",
                                            form.TokenIndex,
                                            rootForms?.TokenIndex ?? -1);
                                        hasError = true;
                                    }
                                }

                                if (form.Value.Op?.Elements != null && form.Value.Op.Elements.Any(op => op.Value.Value == TDValues.OpReadProp))
                                {
                                    if (readAllForm == null)
                                    {
                                        errorReporter.ReportError(ErrorCondition.Unusable, $"Property '{prop.Key}' has '{TDProperty.FormsName}' element with '{TDForm.OpName}' value of '{TDValues.OpReadProp}' but no '{TDForm.TopicName}' property, so it cannot be read individually; however, there is no root-level form with an '{TDForm.OpName}' property that has value '{TDValues.OpReadAllProps}', so this property also cannot be read in aggregate.",
                                            form.TokenIndex,
                                            rootForms?.TokenIndex ?? -1);
                                        hasError = true;
                                    }
                                    else if (propFormHasAdditionalResponses && !aggregateReadHasAdditionalResponses)
                                    {
                                        errorReporter.ReportError(ErrorCondition.Unusable, $"Property '{prop.Key}' has '{TDProperty.FormsName}' element with '{TDForm.OpName}' value of '{TDValues.OpReadProp}' but no '{TDForm.TopicName}' property, so its '{TDForm.AdditionalResponsesName}' value cannot be returned on an individual read, nor can it be returned on an aggregate read because the root-level form with '{TDForm.OpName}' value of '{TDValues.OpReadAllProps}' has no '{TDForm.AdditionalResponsesName}' value.",
                                            form.TokenIndex,
                                            readAllForm.TokenIndex);
                                        hasError = true;
                                    }
                                }

                                if (form.Value.Op?.Elements != null && form.Value.Op.Elements.Any(op => op.Value.Value == TDValues.OpWriteProp))
                                {
                                    if (writeMultiForm == null)
                                    {
                                        errorReporter.ReportError(ErrorCondition.Unusable, $"Property '{prop.Key}' has '{TDProperty.FormsName}' element with '{TDForm.OpName}' value of '{TDValues.OpWriteProp}' but no '{TDForm.TopicName}' property, so it cannot be written individually; however, there is no root-level form with an '{TDForm.OpName}' property that has value '{TDValues.OpWriteMultProps}', so this property also cannot be written in aggregate.",
                                            form.TokenIndex,
                                            rootForms?.TokenIndex ?? -1);
                                        hasError = true;
                                    }
                                    else if (propFormHasAdditionalResponses && (writeMultiForm.Value.AdditionalResponses?.Elements?.Count ?? 0) == 0)
                                    {
                                        errorReporter.ReportError(ErrorCondition.Unusable, $"Property '{prop.Key}' has '{TDProperty.FormsName}' element with '{TDForm.OpName}' value of '{TDValues.OpWriteProp}' but no '{TDForm.TopicName}' property, so its '{TDForm.AdditionalResponsesName}' value cannot be returned on an individual write, nor can it be returned on an aggregate write because the root-level form with '{TDForm.OpName}' value of '{TDValues.OpWriteMultProps}' has no '{TDForm.AdditionalResponsesName}' value.",
                                            form.TokenIndex,
                                            writeMultiForm.TokenIndex);
                                        hasError = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return !hasError;
        }

        private bool TryValidateCrossFormConsistency(ArrayTracker<TDForm>? rootForms, MapTracker<TDEvent>? events)
        {
            ValueTracker<TDForm>? subAllForm = rootForms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements != null && f.Value.Op.Elements.Any(op => op.Value.Value == TDValues.OpSubAllEvents));

            bool hasError = false;

            if (events?.Entries == null || events.Entries.Count == 0)
            {
                if (subAllForm != null)
                {
                    errorReporter.ReportError(ErrorCondition.Unusable, $"Root-level form has '{TDForm.OpName}' property with value '{TDValues.OpSubAllEvents}' to subscribe to the aggregation of all events, but Thing Description has no events defined.",
                        subAllForm.Value.Op!.Elements!.First(op => op.Value.Value == TDValues.OpSubAllEvents).TokenIndex,
                        events?.TokenIndex ?? -1);
                    hasError = true;
                }
            }
            else if (subAllForm == null)
            {
                foreach (KeyValuePair<string, ValueTracker<TDEvent>> evt in events.Entries)
                {
                    if (!(evt.Value.Value.Forms?.Elements?.Any(f => f.Value.Topic != null) ?? false))
                    {
                        errorReporter.ReportError(ErrorCondition.Unusable, $"Event '{evt.Key}' has no '{TDEvent.FormsName}' element with a '{TDForm.TopicName}' property, so it cannot be subscribed individually; however, there is no root-level form with an '{TDForm.OpName}' property that has value '{TDValues.OpSubAllEvents}', so this event also cannot be subscribed in aggregate.",
                            evt.Value.TokenIndex,
                            rootForms?.TokenIndex ?? -1);
                        hasError = true;
                    }
                }
            }

            return !hasError;
        }

        private bool TryValidateContext(ArrayTracker<TDContextSpecifier>? context)
        {
            bool hasError = false;

            if (context?.Elements == null)
            {
                errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Thing Description is missing required '{TDThing.ContextName}' property.", -1);
                return false;
            }

            bool tdContextPresent = false;
            bool aioContextPresent = false;

            foreach (ValueTracker<TDContextSpecifier> contextSpecifier in context.Elements)
            {
                if (contextSpecifier.Value?.Remote?.Value != null)
                {
                    string remoteContext = contextSpecifier.Value.Remote.Value.Value;
                    if (remoteContext != TdContextUri)
                    {
                        errorReporter.ReportWarning($"Unrecognized remote {TDThing.ContextName} \"{remoteContext}\"; value will be ignored.", contextSpecifier.TokenIndex);
                    }
                    else
                    {
                        tdContextPresent = true;
                    }
                }
                else if (contextSpecifier.Value?.Local?.Entries != null)
                {
                    foreach (KeyValuePair<string, ValueTracker<StringHolder>> localContext in contextSpecifier.Value.Local.Entries)
                    {
                        string prefix = localContext.Key;
                        if (localContext.Key != AioContextPrefix)
                        {
                            errorReporter.ReportWarning($"Unrecognized local {TDThing.ContextName} term \"{localContext.Key}\"; value will be ignored.", contextSpecifier.TokenIndex);
                        }
                        else if (localContext.Value.Value.Value != AioContextUriBase)
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Local {TDThing.ContextName} term \"{localContext.Key}\" has incorrect URI value \"{localContext.Value.Value.Value}\".", contextSpecifier.TokenIndex);
                            hasError = true;
                        }
                        else
                        {
                            aioContextPresent = true;
                        }
                    }
                }
            }

            if (!tdContextPresent)
            {
                errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Thing Description is missing required '{TDThing.ContextName}' remote URI \"{TdContextUri}\".", context.TokenIndex);
                hasError = true;
            }

            if (!aioContextPresent)
            {
                errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Thing Description is missing required '{TDThing.ContextName}' local term \"{AioContextPrefix}\" with URI value \"{AioContextUriBase}\".", context.TokenIndex);
                hasError = true;
            }

            return !hasError;
        }

        private bool TryValidateType(ValueTracker<StringHolder>? type)
        {
            if (type == null)
            {
                errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Thing Description is missing required '{TDThing.TypeName}' property.", -1);
                return false;
            }

            if (string.IsNullOrWhiteSpace(type.Value.Value))
            {
                errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"Thing Description '{TDThing.TypeName}' property has empty value.", type.TokenIndex);
                return false;
            }

            if (type.Value.Value != TDValues.TypeThingModel)
            {
                errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"Thing Description '{TDThing.TypeName}' property value '{type.Value.Value}' is not correct; value must be `{TDValues.TypeThingModel}`.", type.TokenIndex);
                return false;
            }

            return true;
        }

        private bool TryValidateTitle(ValueTracker<StringHolder>? title)
        {
            if (title == null)
            {
                errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Thing Description is missing required '{TDThing.TitleName}' property.", -1);
                return false;
            }

            if (string.IsNullOrWhiteSpace(title.Value.Value))
            {
                errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"Thing Description '{TDThing.TitleName}' property has empty value.", title.TokenIndex);
                return false;
            }

            if (!TitleRegex.IsMatch(title.Value.Value))
            {
                errorReporter?.ReportError(ErrorCondition.PropertyInvalid, $"Thing Description '{TDThing.TitleName}' property value \"{title.Value.Value}\" does not conform to codegen type naming rules -- it must start with an uppercase letter and contain only alphanumeric characters", title.TokenIndex);
                return false;
            }

            return true;
        }

        private bool TryValidateLinks(ArrayTracker<TDLink>? links)
        {
            if (links?.Elements == null)
            {
                return true;
            }

            bool hasError = false;
            int relSchemaNamerCount = 0;

            foreach (ValueTracker<TDLink> link in links.Elements)
            {
                if (link.Value.Rel == null)
                {
                    errorReporter.ReportWarning($"Link element is missing '{TDLink.RelName}' property; element will be ignored.", link.TokenIndex);
                    continue;
                }
                if (link.Value.Rel.Value.Value != LinkRelSchemaNamer)
                {
                    errorReporter.ReportWarning($"Link element {TDLink.RelName} property has unrecognized value '{link.Value.Rel.Value.Value}'; element will be ignored.", link.Value.Rel.TokenIndex);
                    continue;
                }

                relSchemaNamerCount++;

                if (link.Value.Href == null)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Link element with {TDLink.RelName}='{LinkRelSchemaNamer}' is missing required '{TDLink.HrefName}' property.", link.TokenIndex);
                    hasError = true;
                }
                else if (string.IsNullOrWhiteSpace(link.Value.Href.Value.Value))
                {
                    errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"Link element with {TDLink.RelName}='{LinkRelSchemaNamer}' has empty '{TDLink.HrefName}' property value.", link.Value.Href.TokenIndex);
                    hasError = true;
                }

                if (link.Value.Type == null)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Link element with {TDLink.RelName}='{LinkRelSchemaNamer}' is missing required '{TDLink.TypeName}' property.", link.TokenIndex);
                    hasError = true;
                }
                else if (string.IsNullOrWhiteSpace(link.Value.Type.Value.Value))
                {
                    errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"Link element with {TDLink.RelName}='{LinkRelSchemaNamer}' has empty '{TDLink.TypeName}' property value.", link.Value.Type.TokenIndex);
                    hasError = true;
                }
                else if (link.Value.Type.Value.Value != TDValues.ContentTypeJson)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"Link element with {TDLink.RelName}='{LinkRelSchemaNamer}' has '{TDLink.TypeName}' property with unsupported value '{link.Value.Type.Value.Value}'.", link.Value.Type.TokenIndex);
                    hasError = true;
                }

                foreach (KeyValuePair<string, long> propertyName in link.Value.PropertyNames)
                {
                    if (propertyName.Key != TDLink.HrefName && propertyName.Key != TDLink.TypeName && propertyName.Key != TDLink.RelName)
                    {
                        if (propertyName.Key.Contains(':') && !propertyName.Key.StartsWith($"{AioContextPrefix}:"))
                        {
                            errorReporter.ReportWarning($"Link has unrecognized '{propertyName.Key}' property, which will be ignored.", propertyName.Value);
                        }
                        else
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"Link has '{propertyName.Key}' property, which is not supported.", propertyName.Value);
                            hasError = true;
                        }
                    }
                }
            }

            if (relSchemaNamerCount > 1)
            {
                errorReporter.ReportError(ErrorCondition.Duplication, $"Thing Description has multiple links with '{TDLink.RelName}' property value '{LinkRelSchemaNamer}'; only one is allowed.", links.TokenIndex);
                hasError = true;
            }

            return !hasError;
        }

        private bool TryValidateSchemaDefinitions(MapTracker<TDDataSchema>? schemaDefinitions)
        {
            if (schemaDefinitions?.Entries == null)
            {
                return true;
            }

            bool hasError = false;

            foreach (KeyValuePair<string, ValueTracker<TDDataSchema>> schemaDefinition in schemaDefinitions.Entries)
            {
                if (!TryValidateDataSchema(schemaDefinition.Value, null, DataSchemaKind.SchemaDefinition))
                {
                    hasError = true;
                }
            }

            return !hasError;
        }

        private bool TryValidateActions(MapTracker<TDAction>? actions, MapTracker<TDDataSchema>? schemaDefinitions, HashSet<SerializationFormat> serializationFormats)
        {
            if (actions?.Entries == null)
            {
                return true;
            }

            bool hasError = false;

            foreach (KeyValuePair<string, ValueTracker<TDAction>> action in actions.Entries)
            {
                if (!TryValidateAction(action.Key, action.Value, schemaDefinitions, out ValueTracker<StringHolder>? contentType))
                {
                    hasError = true;
                }
                else if (contentType != null)
                {
                    serializationFormats.Add(ThingSupport.ContentTypeToFormat(contentType.Value.Value));
                }
            }

            return !hasError;
        }

        private bool TryValidateAction(string name, ValueTracker<TDAction> action, MapTracker<TDDataSchema>? schemaDefinitions, out ValueTracker<StringHolder>? contentType)
        {
            if (!TryValidateForms(action.Value.Forms, FormsKind.Action, schemaDefinitions, out contentType))
            {
                return false;
            }

            bool hasError = false;

            if (action.Value.Input != null && !TryValidateActionDataSchema(action.Value.Input, TDAction.InputName, contentType))
            {
                hasError = true;
            }

            if (action.Value.Output != null && !TryValidateActionDataSchema(action.Value.Output, TDAction.OutputName, contentType))
            {
                hasError = true;
            }

            foreach (KeyValuePair<string, long> propertyName in action.Value.PropertyNames)
            {
                if (propertyName.Key != TDAction.DescriptionName && propertyName.Key != TDAction.InputName && propertyName.Key != TDAction.OutputName && propertyName.Key != TDAction.IdempotentName && propertyName.Key != TDAction.SafeName && propertyName.Key != TDAction.FormsName)
                {
                    if (propertyName.Key.Contains(':') && !propertyName.Key.StartsWith($"{AioContextPrefix}:"))
                    {
                        errorReporter.ReportWarning($"Action '{name}' has unrecognized '{propertyName.Key}' property, which will be ignored.", propertyName.Value);
                    }
                    else
                    {
                        errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"Action '{name}' has '{propertyName.Key}' property, which is not supported.", propertyName.Value);
                        hasError = true;
                    }
                }
            }

            return !hasError;
        }

        private bool TryValidateActionDataSchema<T>(ValueTracker<T> dataSchema, string propertyName, ValueTracker<StringHolder>? contentType)
            where T : TDDataSchema, IDeserializable<T>
        {
            bool isStructuredObject = dataSchema.Value.Type?.Value.Value == TDValues.TypeObject && dataSchema.Value.Properties != null;
            bool isNull = dataSchema.Value.Type?.Value.Value == TDValues.TypeNull;
            bool isReference = dataSchema.Value.Ref != null;
            if (!isStructuredObject && !isNull && !isReference)
            {
                errorReporter.ReportError(ErrorCondition.TypeMismatch, $"'{TDThing.ActionsName}' element '{propertyName}' property must have a schema of (or a reference to) a structured object type, or no schema at all via a '{TDDataSchema.TypeName}' value of '{TDValues.TypeNull}'.", dataSchema.TokenIndex);
                return false;
            }

            return TryValidateDataSchema(dataSchema, null, DataSchemaKind.Action, contentType);
        }

        private bool TryValidateProperties(MapTracker<TDProperty>? properties, MapTracker<TDDataSchema>? schemaDefinitions, HashSet<SerializationFormat> serializationFormats)
        {
            if (properties?.Entries == null)
            {
                return true;
            }

            bool hasError = false;

            foreach (KeyValuePair<string, ValueTracker<TDProperty>> property in properties.Entries)
            {
                if (!TryValidateProperty(property.Key, property.Value, schemaDefinitions, out ValueTracker<StringHolder>? contentType))
                {
                    hasError = true;
                }
                else if (contentType != null)
                {
                    serializationFormats.Add(ThingSupport.ContentTypeToFormat(contentType.Value.Value));
                }
            }

            return !hasError;
        }

        private bool TryValidateProperty(string name, ValueTracker<TDProperty> property, MapTracker<TDDataSchema>? schemaDefinitions, out ValueTracker<StringHolder>? contentType)
        {
            if (!TryValidatePropertyForms(name, property.Value.Forms, schemaDefinitions, property.Value.ReadOnly, out contentType))
            {
                return false;
            }

            if (!TryValidateDataSchema(property, (propName) => propName == TDProperty.ReadOnlyName || propName == TDProperty.PlaceholderName || propName == TDProperty.FormsName, DataSchemaKind.Property, contentType))
            {
                return false;
            }

            return true;
        }

        private bool TryValidatePropertyForms(string name, ArrayTracker<TDForm>? forms, MapTracker<TDDataSchema>? schemaDefinitions, ValueTracker<BoolHolder>? readOnly, out ValueTracker<StringHolder>? contentType)
        {
            bool isReadOnly = readOnly?.Value.Value == true;

            if (!TryValidateForms(forms, FormsKind.Property, schemaDefinitions, out contentType, isReadOnly))
            {
                return false;
            }

            bool hasError = false;

            List<ValueTracker<StringHolder>> aggregateOps = forms?.Elements?.SelectMany(form => form.Value.Op?.Elements ?? new())?.ToList() ?? new();
            ValueTracker<StringHolder>? writeOp = aggregateOps.FirstOrDefault(op => op.Value.Value == TDValues.OpWriteProp);
            ValueTracker<StringHolder>? readOp = aggregateOps.FirstOrDefault(op => op.Value.Value == TDValues.OpReadProp);

            if (writeOp != null)
            {
                if (isReadOnly)
                {
                    errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"'{TDProperty.FormsName}' array contains '{TDForm.OpName}' property with value '{TDValues.OpWriteProp}' but the property has '{TDProperty.ReadOnlyName}' true.", writeOp.TokenIndex, readOnly?.TokenIndex ?? -1);
                    hasError = true;
                }
                if (readOp == null)
                {
                    errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"'{TDProperty.FormsName}' array contains '{TDForm.OpName}' property with value '{TDValues.OpWriteProp}' but no '{TDForm.OpName}' property with value '{TDValues.OpReadProp}'.", writeOp.TokenIndex, forms?.TokenIndex ?? -1);
                    hasError = true;
                }
                else
                {
                    ValueTracker<TDForm>? topicalWriteForm = forms?.Elements?.FirstOrDefault(form => form.Value.Topic != null && (form.Value.Op?.Elements?.Any(op => op.Value.Value == TDValues.OpWriteProp) ?? false));
                    bool hasTopicalReadForm = forms?.Elements?.Any(form => form.Value.Topic != null && (form.Value.Op?.Elements?.Any(op => op.Value.Value == TDValues.OpReadProp) ?? false)) ?? false;
                    if (topicalWriteForm != null && !hasTopicalReadForm)
                    {
                        errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"'{TDProperty.FormsName}' array contains entry with '{TDForm.TopicName}' property and '{TDForm.OpName}' property with value '{TDValues.OpWriteProp}' but no entry with '{TDForm.TopicName}' property and '{TDForm.OpName}' property with value '{TDValues.OpReadProp}'.", topicalWriteForm.TokenIndex, forms?.TokenIndex ?? -1);
                        hasError = true;
                    }
                }
            }
            else if (readOp != null)
            {
                if (readOnly == null)
                {
                    errorReporter.ReportWarning($"Property '{name}' is effectively read-only because the only '{TDForm.OpName}' value in '{TDProperty.FormsName}' is '{TDValues.OpReadProp}'; however, the property has no '{TDProperty.ReadOnlyName}' property with value true.", readOp.TokenIndex);
                }
                else if (readOnly.Value.Value == false)
                {
                    errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Property '{name}' is effectively read-only because the only '{TDForm.OpName}' value in '{TDProperty.FormsName}' is '{TDValues.OpReadProp}'; however, the property has a '{TDProperty.ReadOnlyName}' property with value false.", readOp.TokenIndex, readOnly.TokenIndex);
                    hasError = true;
                }
            }

            return !hasError;
        }

        private bool TryValidateEvents(MapTracker<TDEvent>? evts, MapTracker<TDDataSchema>? schemaDefinitions, HashSet<SerializationFormat> serializationFormats)
        {
            if (evts?.Entries == null)
            {
                return true;
            }

            bool hasError = false;

            foreach (KeyValuePair<string, ValueTracker<TDEvent>> evt in evts.Entries)
            {
                if (!TryValidateEvent(evt.Key, evt.Value, schemaDefinitions, out ValueTracker<StringHolder>? contentType))
                {
                    hasError = true;
                }
                else if (contentType != null)
                {
                    serializationFormats.Add(ThingSupport.ContentTypeToFormat(contentType.Value.Value));
                }
            }

            return !hasError;
        }

        private bool TryValidateEvent(string name, ValueTracker<TDEvent> evt, MapTracker<TDDataSchema>? schemaDefinitions, out ValueTracker<StringHolder>? contentType)
        {
            if (!TryValidateForms(evt.Value.Forms, FormsKind.Event, schemaDefinitions, out contentType))
            {
                return false;
            }

            bool hasError = false;

            if (evt.Value.Data != null && !TryValidateDataSchema(evt.Value.Data, null, DataSchemaKind.Event, contentType))
            {
                hasError = true;
            }

            foreach (KeyValuePair<string, long> propertyName in evt.Value.PropertyNames)
            {
                if (propertyName.Key != TDEvent.DescriptionName && propertyName.Key != TDEvent.DataName && propertyName.Key != TDEvent.PlaceholderName && propertyName.Key != TDEvent.FormsName)
                {
                    if (propertyName.Key.Contains(':') && !propertyName.Key.StartsWith($"{AioContextPrefix}:"))
                    {
                        errorReporter.ReportWarning($"Event '{name}' has unrecognized '{propertyName.Key}' property, which will be ignored.", propertyName.Value);
                    }
                    else
                    {
                        errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"Event '{name}' has '{propertyName.Key}' property, which is not supported.", propertyName.Value);
                        hasError = true;
                    }
                }
            }

            return !hasError;
        }

        private bool TryValidateForms(ArrayTracker<TDForm>? forms, FormsKind formsKind, MapTracker<TDDataSchema>? schemaDefinitions, out ValueTracker<StringHolder>? contentType, bool isReadOnly = false)
        {
            contentType = null;

            if (forms?.Elements == null)
            {
                return true;
            }

            if (forms.Elements!.Count == 0)
            {
                errorReporter.ReportError(ErrorCondition.ElementMissing, $"Property '{TDEvent.FormsName}' array value contains no elements; at least one form is required.", forms.TokenIndex);
                return false;
            }

            bool hasError = false;

            foreach (ValueTracker<TDForm> form in forms.Elements)
            {
                if (!TryValidateForm(form, formsKind, schemaDefinitions, out ValueTracker<StringHolder>? formContentType, isReadOnly))
                {
                    hasError = true;
                }
                else if (formContentType != null)
                {
                    if (contentType != null)
                    {
                        if (formContentType.Value.Value != contentType.Value.Value)
                        {
                            errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"'{TDThing.FormsName}' array contains forms with different '{TDForm.ContentTypeName}' property values '{contentType.Value.Value}' and '{formContentType.Value.Value}'.", contentType.TokenIndex, formContentType.TokenIndex);
                            hasError = true;
                        }
                    }
                    else
                    {
                        contentType = formContentType;
                    }
                }
            }

            if (hasError)
            {
                return false;
            }

            ValueTracker<TDForm>? oplessForm = forms.Elements.FirstOrDefault(f => f.Value.Op == null);
            if (oplessForm != null && forms.Elements.Count > 1)
            {
                errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"'{TDThing.FormsName}' array contains a form with no '{TDForm.OpName}' property, so it must be the only form in the array.", oplessForm.TokenIndex, forms.TokenIndex);
                return false;
            }

            List<ValueTracker<StringHolder>> aggregateOps = forms.Elements.SelectMany(form => form.Value.Op?.Elements ?? new()).ToList();

            foreach (IGrouping<string, ValueTracker<StringHolder>> dupOpGroup in aggregateOps.GroupBy(op => op.Value.Value).Where(g => g.Count() > 1))
            {
                errorReporter.ReportError(ErrorCondition.Duplication, $"'{TDThing.FormsName}' array contains '{TDForm.OpName}' properties that duplicate value '{dupOpGroup.Key}'.", dupOpGroup.First().TokenIndex, dupOpGroup.Skip(1).First().TokenIndex);
                hasError = true;
            }

            return !hasError;
        }

        private bool TryValidateForm(ValueTracker<TDForm> form, FormsKind formsKind, MapTracker<TDDataSchema>? schemaDefinitions, out ValueTracker<StringHolder>? contentType, bool isReadOnly)
        {
            bool hasError = false;

            if (form.Value.Op?.Elements == null)
            {
                if (formsKind == FormsKind.Root)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Root-level form is missing required '{TDForm.OpName}' property.", form.TokenIndex);
                    hasError = true;
                }
            }
            else if (form.Value.Op.Elements.Count == 0)
            {
                errorReporter.ReportError(ErrorCondition.ElementMissing, $"Form '{TDForm.OpName}' property has no values.", form.Value.Op.TokenIndex);
                hasError = true;
            }

            if (form.Value.ContentType != null)
            {
                if (formsKind == FormsKind.Action || formsKind == FormsKind.Event)
                {
                    if (form.Value.ContentType.Value.Value != TDValues.ContentTypeJson && form.Value.ContentType.Value.Value != TDValues.ContentTypeRaw && form.Value.ContentType.Value.Value != TDValues.ContentTypeCustom)
                    {
                        errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"Form '{TDForm.ContentTypeName}' property has unsupported value '{form.Value.ContentType.Value.Value}'; for '{TDThing.ActionsName}' and '{TDThing.EventsName}' forms, supported values are '{TDValues.ContentTypeJson}', '{TDValues.ContentTypeRaw}', and '{TDValues.ContentTypeCustom}' (empty string).", form.Value.ContentType.TokenIndex);
                        hasError = true;
                    }
                }
                else
                {
                    if (form.Value.ContentType.Value.Value != TDValues.ContentTypeJson)
                    {
                        string adjective = form.Value.ContentType.Value.Value == TDValues.ContentTypeRaw || form.Value.ContentType.Value.Value == TDValues.ContentTypeCustom ? "disallowed" : "unsupported";
                        errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"Form '{TDForm.ContentTypeName}' property has {adjective} value '{form.Value.ContentType.Value.Value}'; for '{TDThing.PropertiesName}' and root-level forms, only '{TDValues.ContentTypeJson}' is supported.", form.Value.ContentType.TokenIndex);
                        hasError = true;
                    }
                }
            }

            if (form.Value.ServiceGroupId != null)
            {
                if (formsKind == FormsKind.Root && !(form.Value.Op?.Elements?.Any(op => op.Value.Value == TDValues.OpSubAllEvents) ?? false))
                {
                    errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"'{TDForm.ServiceGroupIdName}' property is not allowed in root-level '{TDThing.FormsName}' property without an '{TDForm.OpName}' property value of '{TDValues.OpSubAllEvents}'.", form.Value.ServiceGroupId.TokenIndex);
                    hasError = true;
                }
                else if (formsKind == FormsKind.Property)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"'{TDForm.ServiceGroupIdName}' property is not allowed in '{TDProperty.FormsName}' property of a '{TDThing.PropertiesName}' element.", form.Value.ServiceGroupId.TokenIndex);
                    hasError = true;
                }
                else if (string.IsNullOrWhiteSpace(form.Value.ServiceGroupId.Value.Value))
                {
                    errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"Form '{TDForm.ServiceGroupIdName}' property has empty value.", form.Value.ServiceGroupId.TokenIndex);
                    hasError = true;
                }
            }

            if (hasError)
            {
                contentType = null;
                return false;
            }

            foreach (ValueTracker<StringHolder> op in form.Value.Op?.Elements ?? new())
            {
                if (string.IsNullOrWhiteSpace(op.Value.Value))
                {
                    errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"Form '{TDForm.OpName}' property has empty value.", op.TokenIndex);
                    hasError = true;
                    continue;
                }

                switch (formsKind)
                {
                    case FormsKind.Root:
                        if (op.Value.Value != TDValues.OpReadAllProps && op.Value.Value != TDValues.OpWriteMultProps && op.Value.Value != TDValues.OpSubAllEvents)
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"Form '{TDForm.OpName}' property has unsupported value '{op.Value.Value}' for a root-level form; supported values are '{TDValues.OpReadAllProps}', '{TDValues.OpWriteMultProps}', and '{TDValues.OpSubAllEvents}'.", op.TokenIndex);
                            hasError = true;
                        }
                        break;
                    case FormsKind.Property:
                        if (op.Value.Value != TDValues.OpReadProp && op.Value.Value != TDValues.OpWriteProp)
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"Form '{TDForm.OpName}' property has unsupported value '{op.Value.Value}' for a property form; supported values are '{TDValues.OpReadProp}' and '{TDValues.OpWriteProp}'.", op.TokenIndex);
                            hasError = true;
                        }
                        break;
                    case FormsKind.Action:
                        if (op.Value.Value != TDValues.OpInvokeAction)
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"Form '{TDForm.OpName}' property has unsupported value '{op.Value.Value}' for an action form; only supported value is '{TDValues.OpInvokeAction}'.", op.TokenIndex);
                            hasError = true;
                        }
                        break;
                    case FormsKind.Event:
                        if (op.Value.Value != TDValues.OpSubEvent)
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"Form '{TDForm.OpName}' property has unsupported value '{op.Value.Value}' for an event form; only supported value is '{TDValues.OpSubEvent}'.", op.TokenIndex);
                            hasError = true;
                        }
                        break;
                }
            }

            if (hasError)
            {
                contentType = null;
                return false;
            }

            bool hasOpRead = form.Value.Op?.Elements?.Any(op => op.Value.Value == TDValues.OpReadProp || op.Value.Value == TDValues.OpReadAllProps) ?? false;
            bool hasOpWrite = form.Value.Op?.Elements?.Any(op => op.Value.Value == TDValues.OpWriteProp || op.Value.Value == TDValues.OpWriteMultProps) ?? false;
            bool hasOpSub = form.Value.Op?.Elements?.Any(op => op.Value.Value == TDValues.OpSubEvent || op.Value.Value == TDValues.OpSubAllEvents) ?? false;

            if (form.Value.Op?.Elements != null)
            {
                foreach (IGrouping<string, ValueTracker<StringHolder>> dupOpGroup in form.Value.Op.Elements.GroupBy(op => op.Value.Value).Where(g => g.Count() > 1))
                {
                    errorReporter.ReportError(ErrorCondition.Duplication, $"Form '{TDForm.OpName}' property has duplicate value '{dupOpGroup.Key}'.", form.Value.Op.TokenIndex);
                    hasError = true;
                }

                if (formsKind == FormsKind.Root)
                {
                    if (hasOpRead && hasOpSub)
                    {
                        errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"A single form '{TDForm.OpName}' property cannot contain both '{TDValues.OpReadAllProps}' and '{TDValues.OpSubAllEvents}' values.", form.Value.Op.TokenIndex);
                        hasError = true;
                    }
                    if (hasOpWrite && hasOpSub)
                    {
                        errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Form '{TDForm.OpName}' property cannot contain both '{TDValues.OpWriteMultProps}' and '{TDValues.OpSubAllEvents}' values.", form.Value.Op.TokenIndex);
                        hasError = true;
                    }
                }
            }

            if (form.Value.Topic == null)
            {
                if (formsKind == FormsKind.Root)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Root-level form is missing required '{TDForm.TopicName}' property.", form.TokenIndex);
                    hasError = true;
                }
            }
            else
            {
                if (!TryValidateTopic(form.Value.Topic, formsKind, hasOpRead, hasOpWrite, hasOpSub, isReadOnly))
                {
                    hasError = true;
                }

                if (form.Value.ContentType == null)
                {
                    errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Form missing '{TDForm.ContentTypeName}' property, which is required when '{TDForm.TopicName}' property present.", form.TokenIndex, form.Value.Topic.TokenIndex);
                    hasError = true;
                }
            }

            if (form.Value.HeaderCode != null)
            {
                if (formsKind != FormsKind.Action)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"Form '{TDForm.HeaderCodeName}' property is permitted only in action forms.", form.Value.HeaderCode.TokenIndex);
                    hasError = true;
                }
                else if (string.IsNullOrWhiteSpace(form.Value.HeaderCode.Value.Value))
                {
                    errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"Form '{TDForm.HeaderCodeName}' property has empty value.", form.Value.HeaderCode.TokenIndex);
                    hasError = true;
                }
                else if (schemaDefinitions?.Entries == null)
                {
                    errorReporter.ReportError(ErrorCondition.ItemNotFound, $"Form '{TDForm.HeaderCodeName}' property must refer to key in '{TDThing.SchemaDefinitionsName}' property, but TD has no '{TDThing.SchemaDefinitionsName}' property.", form.Value.HeaderCode.TokenIndex);
                    hasError = true;
                }
                else if (!schemaDefinitions.Entries.TryGetValue(form.Value.HeaderCode.Value.Value, out ValueTracker<TDDataSchema>? dataSchema))
                {
                    errorReporter.ReportError(ErrorCondition.ItemNotFound, $"Form '{TDForm.HeaderCodeName}' property refers to non-existent key '{form.Value.HeaderCode.Value.Value}' in '{TDThing.SchemaDefinitionsName}' property.", form.Value.HeaderCode.TokenIndex, schemaDefinitions.TokenIndex);
                    hasError = true;
                }
                else if (dataSchema.Value.Type?.Value.Value != TDValues.TypeString || dataSchema.Value.Enum == null)
                {
                    errorReporter.ReportError(ErrorCondition.TypeMismatch, $"Form '{TDForm.HeaderCodeName}' property refers to '{TDThing.SchemaDefinitionsName}' key '{form.Value.HeaderCode.Value.Value}', but this is not a string enum.", form.Value.HeaderCode.TokenIndex, dataSchema.TokenIndex);
                    hasError = true;
                }
            }

            if (form.Value.AdditionalResponses?.Elements != null)
            {
                if (formsKind == FormsKind.Event)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"Form '{TDForm.AdditionalResponsesName}' property is not permitted in an event form.", form.Value.AdditionalResponses.TokenIndex);
                    hasError = true;
                }
                else if (formsKind == FormsKind.Root && !form.Value.Op!.Elements!.Any(op => op.Value.Value == TDValues.OpReadAllProps || op.Value.Value == TDValues.OpWriteMultProps))
                {
                    errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Form '{TDForm.AdditionalResponsesName}' property is not permitted in a root form without an '{TDForm.OpName}' property value of '{TDValues.OpReadAllProps}' or '{TDValues.OpWriteMultProps}'.", form.Value.AdditionalResponses.TokenIndex, form.Value.Op.TokenIndex);
                    hasError = true;
                }
                else if (!TryValidateSchemaReferences(form.Value.AdditionalResponses, formsKind, TDForm.AdditionalResponsesName, schemaDefinitions))
                {
                    hasError = true;
                }
            }

            if (form.Value.HeaderInfo?.Elements != null)
            {
                if (formsKind != FormsKind.Action)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"Form '{TDForm.HeaderInfoName}' property is permitted only in an action form.", form.Value.HeaderInfo.TokenIndex);
                    hasError = true;
                }
                else if (!TryValidateSchemaReferences(form.Value.HeaderInfo, formsKind, TDForm.HeaderInfoName, schemaDefinitions))
                {
                    hasError = true;
                }
            }

            foreach (KeyValuePair<string, long> propertyName in form.Value.PropertyNames)
            {
                if (propertyName.Key != TDForm.ContentTypeName && propertyName.Key != TDForm.AdditionalResponsesName && propertyName.Key != TDForm.HeaderInfoName && propertyName.Key != TDForm.HeaderCodeName && propertyName.Key != TDForm.ServiceGroupIdName && propertyName.Key != TDForm.TopicName && propertyName.Key != TDForm.OpName)
                {
                    if (propertyName.Key.Contains(':') && !propertyName.Key.StartsWith($"{AioContextPrefix}:"))
                    {
                        errorReporter.ReportWarning($"Form has unrecognized '{propertyName.Key}' property, which will be ignored.", propertyName.Value);
                    }
                    else
                    {
                        errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"Form has '{propertyName.Key}' property, which is not supported.", propertyName.Value);
                        hasError = true;
                    }
                }
            }

            contentType = form.Value.ContentType;
            return !hasError;
        }

        private bool TryValidateTopic(ValueTracker<StringHolder> topic, FormsKind formsKind, bool hasOpRead, bool hasOpWrite, bool hasOpSub, bool isReadOnly)
        {
            FormsKind effectiveFormsKind = formsKind switch
            {
                FormsKind.Action => FormsKind.Action,
                FormsKind.Property => FormsKind.Property,
                FormsKind.Event => FormsKind.Event,
                FormsKind.Root when hasOpRead || hasOpWrite => FormsKind.Property,
                FormsKind.Root when hasOpSub => FormsKind.Event,
                _ => FormsKind.Root,
            };

            bool hasError = false;

            if (topic.Value.Value.Length == 0)
            {
                errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"Form '{TDForm.TopicName}' property has empty value.", topic.TokenIndex);
                return false;
            }

            if (topic.Value.Value.StartsWith('$'))
            {
                errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Form '{TDForm.TopicName}' property value starts with reserved character '$'.", topic.TokenIndex);
                hasError = true;
            }

            bool actionTokenPresent = false;
            foreach (string level in topic.Value.Value.Split('/'))
            {
                if (level.Length == 0)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Form '{TDForm.TopicName}' property has value containing empty topic level.", topic.TokenIndex);
                    hasError = true;
                }
                else if (level.StartsWith('{') && level.EndsWith('}'))
                {
                    string token = level[1..^1];
                    if (token.Length == 0)
                    {
                        errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Form '{TDForm.TopicName}' property has value containing empty token '{{}}'.", topic.TokenIndex);
                        hasError = true;
                    }
                    else if (token.StartsWith($"{MqttTopicTokens.PrefixCustom}"))
                    {
                        string exToken = token[3..];
                        if (exToken.Length == 0)
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Form '{TDForm.TopicName}' property has value containing empty custom token '{{{MqttTopicTokens.PrefixCustom}}}'.", topic.TokenIndex);
                            hasError = true;
                        }
                        else if (!exToken.All(c => char.IsAsciiLetter(c)))
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Form '{TDForm.TopicName}' property has value containing custom token '{{{token}}}' that contains invalid character(s); only ASCII letters are allowed after the '{MqttTopicTokens.PrefixCustom}' prefix.", topic.TokenIndex);
                            hasError = true;
                        }
                    }
                    else
                    {
                        switch (effectiveFormsKind)
                        {
                            case FormsKind.Action:
                                if (token != MqttTopicTokens.ActionInvokerId && token != MqttTopicTokens.ActionExecutorId)
                                {
                                    errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Form '{TDForm.TopicName}' property has value containing token '{{{token}}}' that is not valid in an action topic; only '{{{MqttTopicTokens.ActionInvokerId}}}' and '{{{MqttTopicTokens.ActionExecutorId}}}' are allowed unless token starts with '{MqttTopicTokens.PrefixCustom}'.", topic.TokenIndex);
                                    hasError = true;
                                }
                                break;
                            case FormsKind.Property:
                                if (token == MqttTopicTokens.PropertyAction)
                                {
                                    actionTokenPresent = true;
                                }
                                else if (token != MqttTopicTokens.PropertyConsumerId && token != MqttTopicTokens.PropertyMaintainerId)
                                {
                                    errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Form '{TDForm.TopicName}' property has value containing token '{{{token}}}' that is not valid in a property topic; only '{{{MqttTopicTokens.PropertyAction}}}', '{{{MqttTopicTokens.PropertyConsumerId}}}', and '{{{MqttTopicTokens.PropertyMaintainerId}}}' are allowed unless token starts with '{MqttTopicTokens.PrefixCustom}'.", topic.TokenIndex);
                                    hasError = true;
                                }
                                break;
                            case FormsKind.Event:
                                if (token != MqttTopicTokens.EventSenderId)
                                {
                                    errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Form '{TDForm.TopicName}' property has value containing token '{{{token}}}' that is not valid in an event topic; only '{{{MqttTopicTokens.EventSenderId}}}' is allowed unless token starts with '{MqttTopicTokens.PrefixCustom}'.", topic.TokenIndex);
                                    hasError = true;
                                }
                                break;
                        }
                    }
                }
                else
                {
                    if (level.Any(c => c is < '!' or > '~' or '+' or '#' or '{' or '}'))
                    {
                        errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Form '{TDForm.TopicName}' property has value containing non-token topic level '{level}' that contains invalid character(s); only printable ASCII characters not including space, '\"', '+', '#', '{{', or '}}' are allowed.", topic.TokenIndex);
                        hasError = true;
                    }
                }
            }

            if (effectiveFormsKind == FormsKind.Property)
            {
                if (actionTokenPresent)
                {
                    string readResolvedTopic = topic.Value.Value.Replace($"{{{MqttTopicTokens.PropertyAction}}}", MqttTopicTokens.PropertyActionValues.Read);
                    string writeResolvedTopic = topic.Value.Value.Replace($"{{{MqttTopicTokens.PropertyAction}}}", MqttTopicTokens.PropertyActionValues.Write);

                    if (hasOpRead)
                    {
                        errorReporter.RegisterTopicInThing(readResolvedTopic, topic.TokenIndex, topic.Value.Value);
                    }

                    if (hasOpWrite)
                    {
                        errorReporter.RegisterTopicInThing(writeResolvedTopic, topic.TokenIndex, topic.Value.Value);
                    }

                    if (!hasOpRead && !hasOpWrite)
                    {
                        errorReporter.RegisterTopicInThing(readResolvedTopic, topic.TokenIndex, topic.Value.Value);
                        if (!isReadOnly)
                        {
                            errorReporter.RegisterTopicInThing(writeResolvedTopic, topic.TokenIndex, topic.Value.Value);
                        }
                    }
                }
                else
                {
                    if ((hasOpRead && hasOpWrite) || (!hasOpRead && !hasOpWrite && !isReadOnly))
                    {
                        errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Form '{TDForm.TopicName}' property value is missing required '{{{MqttTopicTokens.PropertyAction}}}' token for a property form that supports both read and write operations.", topic.TokenIndex);
                        hasError = true;
                    }
                    else
                    {
                        errorReporter.RegisterTopicInThing(topic.Value.Value, topic.TokenIndex, topic.Value.Value);
                    }
                }
            }
            else
            {
                errorReporter.RegisterTopicInThing(topic.Value.Value, topic.TokenIndex, topic.Value.Value);
            }

            return !hasError;
        }

        private bool TryValidateSchemaReferences(ArrayTracker<TDSchemaReference> schemaReferences, FormsKind formsKind, string propertyName, MapTracker<TDDataSchema>? schemaDefinitions)
        {
            bool hasError = false;

            foreach (ValueTracker<TDSchemaReference> schemaReference in schemaReferences.Elements!)
            {
                if (!TryValidateSchemaReference(schemaReference, formsKind, propertyName, schemaDefinitions))
                {
                    hasError = true;
                }
            }

            if (schemaReferences.Elements.Count > 1)
            {
                errorReporter.ReportError(ErrorCondition.ElementsPlural, $"No more than one element is permitted in '{propertyName}' array.", schemaReferences.TokenIndex);
                hasError = true;
            }

            return !hasError;
        }

        private bool TryValidateSchemaReference(ValueTracker<TDSchemaReference> schemaReference, FormsKind formsKind, string parentPropName, MapTracker<TDDataSchema>? schemaDefinitions)
        {
            bool hasError = false;

            if (schemaReference.Value.ContentType == null)
            {
                if (parentPropName == TDForm.HeaderInfoName)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyMissing, $"'{parentPropName}' element is missing required '{TDSchemaReference.ContentTypeName}' property.", schemaReference.TokenIndex);
                    hasError = true;
                }
            }
            else if (string.IsNullOrWhiteSpace(schemaReference.Value.ContentType.Value.Value))
            {
                errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"'{parentPropName}' element has empty '{TDSchemaReference.ContentTypeName}' property value.", schemaReference.Value.ContentType.TokenIndex);
                hasError = true;
            }
            else if (schemaReference.Value.ContentType.Value.Value != TDValues.ContentTypeJson)
            {
                errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"'{parentPropName}' element has '{TDSchemaReference.ContentTypeName}' property with unsupported value '{schemaReference.Value.ContentType.Value.Value}'.", schemaReference.Value.ContentType.TokenIndex);
                hasError = true;
            }

            if (schemaReference.Value.Success == null)
            {
                if (parentPropName == TDForm.AdditionalResponsesName)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyMissing, $"'{parentPropName}' element is missing required '{TDSchemaReference.SuccessName}' property.", schemaReference.TokenIndex);
                    hasError = true;
                }
            }
            else if (schemaReference.Value.Success.Value.Value == true)
            {
                errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"'{parentPropName}' element has '{TDSchemaReference.SuccessName}' property value of true, which is not supported.", schemaReference.Value.Success.TokenIndex);
                hasError = true;
            }

            if (schemaReference.Value.Schema == null)
            {
                if (formsKind != FormsKind.Root)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyMissing, $"'{parentPropName}' element is missing '{TDSchemaReference.SchemaName}' property, which is required except within root-level '{TDThing.FormsName}' elements.", schemaReference.TokenIndex);
                    hasError = true;
                }
            }
            else if (formsKind == FormsKind.Root)
            {
                errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"'{parentPropName}' element has '{TDSchemaReference.SchemaName}' property, which is not allowed within root-level '{TDThing.FormsName}' element because schema is automatically composed from schema definitions in affordance '{TDThing.FormsName}' elements.", schemaReference.Value.Schema.TokenIndex);
                hasError = true;
            }
            else if (string.IsNullOrWhiteSpace(schemaReference.Value.Schema.Value.Value))
            {
                errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"'{parentPropName}' element '{TDSchemaReference.SchemaName}' property has empty value.", schemaReference.Value.Schema.TokenIndex);
                hasError = true;
            }
            else if (schemaDefinitions?.Entries == null)
            {
                errorReporter.ReportError(ErrorCondition.ItemNotFound, $"'{parentPropName}' element '{TDSchemaReference.SchemaName}' property must refer to key in '{TDThing.SchemaDefinitionsName}' property, but TD has no '{TDThing.SchemaDefinitionsName}' property.", schemaReference.Value.Schema.TokenIndex);
                hasError = true;
            }
            else if (!schemaDefinitions.Entries.TryGetValue(schemaReference.Value.Schema.Value.Value, out ValueTracker<TDDataSchema>? dataSchema))
            {
                errorReporter.ReportError(ErrorCondition.ItemNotFound, $"'{parentPropName}' element '{TDSchemaReference.SchemaName}' property refers to non-existent key '{schemaReference.Value.Schema.Value.Value}' in '{TDThing.SchemaDefinitionsName}' property.", schemaReference.Value.Schema.TokenIndex, schemaDefinitions.TokenIndex);
                hasError = true;
            }
            else if (dataSchema.Value.Type?.Value.Value != TDValues.TypeObject)
            {
                errorReporter.ReportError(ErrorCondition.TypeMismatch, $"'{parentPropName}' element '{TDSchemaReference.SchemaName}' property refers to '{TDThing.SchemaDefinitionsName}' key '{schemaReference.Value.Schema.Value.Value}', but this is not a structured object definition.", schemaReference.Value.Schema.TokenIndex, dataSchema.TokenIndex);
                hasError = true;
            }
            else if (dataSchema.Value.Properties == null)
            {
                errorReporter.ReportError(ErrorCondition.TypeMismatch, $"'{parentPropName}' element '{TDSchemaReference.SchemaName}' property refers to '{TDThing.SchemaDefinitionsName}' key '{schemaReference.Value.Schema.Value.Value}', but this defines a map, whereas a structured object is required.", schemaReference.Value.Schema.TokenIndex, dataSchema.TokenIndex);
                hasError = true;
            }

            foreach (KeyValuePair<string, long> propertyName in schemaReference.Value.PropertyNames)
            {
                if (propertyName.Key != TDSchemaReference.SuccessName && propertyName.Key != TDSchemaReference.ContentTypeName && propertyName.Key != TDSchemaReference.SchemaName)
                {
                    if (propertyName.Key.Contains(':') && !propertyName.Key.StartsWith($"{AioContextPrefix}:"))
                    {
                        errorReporter.ReportWarning($"Schema reference has unrecognized '{propertyName.Key}' property, which will be ignored.", propertyName.Value);
                    }
                    else
                    {
                        errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"Schema reference has '{propertyName.Key}' property, which is not supported.", propertyName.Value);
                        hasError = true;
                    }
                }
            }

            return !hasError;
        }

        private bool TryValidateDataSchema<T>(ValueTracker<T> dataSchema, Func<string, bool>? propertyApprover, DataSchemaKind dataSchemaKind = DataSchemaKind.Undistinguished, ValueTracker<StringHolder>? contentType = null)
            where T : TDDataSchema, IDeserializable<T>
        {
            if (dataSchema.Value.Ref != null && dataSchemaKind != DataSchemaKind.Action && dataSchemaKind != DataSchemaKind.Property && dataSchemaKind != DataSchemaKind.Event)
            {
                errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"The '{TDDataSchema.RefName}' property is permitted only in the first level of an affordance schema definition ('{TDThing.PropertiesName}', '{TDThing.EventsName}'/'{TDEvent.DataName}', '{TDThing.ActionsName}'/'{TDAction.InputName}', and '{TDThing.ActionsName}'/'{TDAction.OutputName}').", dataSchema.Value.Ref.TokenIndex);
                return false;
            }

            if (dataSchema.Value.Ref == null && dataSchema.Value.Type == null)
            {
                errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Data schema must have either '{TDDataSchema.RefName}' or '{TDDataSchema.TypeName}' property.", dataSchema.TokenIndex);
                return false;
            }

            if (dataSchema.Value.Ref != null && dataSchema.Value.Type != null)
            {
                errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Data schema cannot have both '{TDDataSchema.RefName}' and '{TDDataSchema.TypeName}' properties.", dataSchema.Value.Ref.TokenIndex, dataSchema.Value.Type.TokenIndex);
                return false;
            }

            bool contentTypeIsRawOrCustom = contentType?.Value.Value == TDValues.ContentTypeRaw || contentType?.Value.Value == TDValues.ContentTypeCustom;

            if (dataSchema.Value.Ref != null)
            {
                if (contentTypeIsRawOrCustom)
                {
                    errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Data schema with '{TDDataSchema.RefName}' property is not permitted in an affordance with a form that specifies '{TDForm.ContentTypeName}' of '{contentType!.Value.Value}'.", dataSchema.Value.Ref.TokenIndex, contentType!.TokenIndex);
                    return false;
                }
                else
                {
                    return TryValidateReferenceDataSchema(dataSchema, propertyApprover);
                }
            }

            if (contentTypeIsRawOrCustom && dataSchema.Value.Type!.Value.Value != TDValues.TypeNull)
            {
                errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Data schema with '{TDDataSchema.TypeName}' of '{dataSchema.Value.Type.Value.Value}' is not permitted in an affordance with a form that specifies '{TDForm.ContentTypeName}' of '{contentType!.Value.Value}'; only '{TDDataSchema.TypeName}' of '{TDValues.TypeNull}' is permitted.", dataSchema.Value.Type.TokenIndex, contentType!.TokenIndex);
                return false;
            }

            switch (dataSchema.Value.Type!.Value.Value)
            {
                case TDValues.TypeObject:
                    return TryValidateObjectDataSchema(dataSchema, dataSchemaKind, propertyApprover);
                case TDValues.TypeArray:
                    return TryValidateArrayDataSchema(dataSchema, propertyApprover);
                case TDValues.TypeString:
                    return TryValidateStringDataSchema(dataSchema, dataSchemaKind, propertyApprover);
                case TDValues.TypeNumber:
                    return TryValidateNumberDataSchema(dataSchema, dataSchemaKind, propertyApprover);
                case TDValues.TypeInteger:
                    return TryValidateIntegerDataSchema(dataSchema, dataSchemaKind, propertyApprover);
                case TDValues.TypeBoolean:
                    return TryValidateBooleanDataSchema(dataSchema, dataSchemaKind, propertyApprover);
                case TDValues.TypeNull:
                    return TryValidateNullDataSchema(dataSchema, dataSchemaKind, propertyApprover, contentTypeIsRawOrCustom, contentType?.TokenIndex ?? -1);
                default:
                    errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"Data schema '{TDDataSchema.TypeName}' property has unsupported value '{dataSchema.Value.Type.Value.Value}'.", dataSchema.Value.Type.TokenIndex);
                    return false;
            }
        }

        private bool TryValidateReferenceDataSchema<T>(ValueTracker<T> dataSchema, Func<string, bool>? propertyApprover)
             where T : TDDataSchema, IDeserializable<T>
        {
            bool hasError = false;

            string refValue = dataSchema.Value.Ref!.Value.Value;
            long tokenIndex = dataSchema.Value.Ref.TokenIndex;

            if (string.IsNullOrWhiteSpace(refValue))
            {
                errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"Data schema '{TDDataSchema.RefName}' property has empty value.", tokenIndex);
                hasError = true;
            }
            else if (!RefCharRegex.IsMatch(refValue))
            {
                errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Data schema '{TDDataSchema.RefName}' property value \"{refValue}\" contains one or more illegal characters.", tokenIndex);
                hasError = true;
            }
            else if (refValue.StartsWith('#'))
            {
                errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Data schema '{TDDataSchema.RefName}' property value \"{refValue}\" may not begin with a '#' character.", tokenIndex);
                hasError = true;
            }
            else
            {
                bool isRelative = refValue.StartsWith("./") || refValue.StartsWith("../");
                bool hasPathSegs = refValue.Contains('/') && refValue.IndexOf('/') < (refValue.Contains('#') ? refValue.IndexOf('#') : refValue.Length);
                if (!isRelative && hasPathSegs)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Data schema '{TDDataSchema.RefName}' property value \"{refValue}\" must be relative (start with \"./\" or \"../\") if it has any path segments.", tokenIndex);
                    hasError = true;
                }
            }

            HashSet<string> supportedProperties = new()
            {
                TDDataSchema.RefName,
                TDDataSchema.TitleName,
                TDDataSchema.DescriptionName,
            };
            if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, propertyApprover, "a schema via a reference", tokenIndex))
            {
                hasError = true;
            }

            return !hasError;
        }

        private bool TryValidateStringConst<T>(ValueTracker<T> dataSchema, ValueTracker<ObjectHolder> constProperty, ValueTracker<ObjectHolder> constValue)
             where T : TDDataSchema, IDeserializable<T>
        {
            if (dataSchema.Value.Title != null && !TitleRegex.IsMatch(dataSchema.Value.Title.Value.Value))
            {
                errorReporter.ReportWarning($"Data schema '{TDDataSchema.TitleName}' property value \"{dataSchema.Value.Title.Value.Value}\" does not conform to codegen type naming rules (only alphanumerics, starting with uppercase), which will be problematic unless titles are suppressed via a 'service-desc' linked schema naming file", dataSchema.Value.Title.TokenIndex);
            }

            bool hasError = false;

            if (constValue.Value.Value is not string)
            {
                errorReporter.ReportError(ErrorCondition.TypeMismatch, $"The specified constant value must be a string.", constValue.TokenIndex, dataSchema.Value.Type!.TokenIndex);
                hasError = true;
            }

            HashSet<string> supportedProperties = new()
            {
                TDDataSchema.TypeName,
                TDDataSchema.TitleName,
                TDDataSchema.DescriptionName,
                TDDataSchema.ConstName,
            };
            if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, null, "a constant string schema", constProperty.TokenIndex))
            {
                hasError = true;
            }

            return !hasError;
        }

        private bool TryValidateNumberConst<T>(ValueTracker<T> dataSchema, ValueTracker<ObjectHolder> constProperty, ValueTracker<ObjectHolder> constValue)
             where T : TDDataSchema, IDeserializable<T>
        {
            bool hasError = false;

            if (dataSchema.Value.Title != null && !TitleRegex.IsMatch(dataSchema.Value.Title.Value.Value))
            {
                errorReporter.ReportWarning($"Data schema '{TDDataSchema.TitleName}' property value \"{dataSchema.Value.Title.Value.Value}\" does not conform to codegen type naming rules (only alphanumerics, starting with uppercase), which will be problematic unless titles are suppressed via a 'service-desc' linked schema naming file", dataSchema.Value.Title.TokenIndex);
            }

            if (constValue.Value.Value is double numValue)
            {
                if (dataSchema.Value.Minimum?.Value.Value != null && numValue < dataSchema.Value.Minimum.Value.Value)
                {
                    errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"The specified constant value ({numValue}) cannot be less than the '{TDDataSchema.MinimumName}' property value ({dataSchema.Value.Minimum.Value.Value}).", constValue.TokenIndex, dataSchema.Value.Minimum.TokenIndex);
                    hasError = true;
                }
                if (dataSchema.Value.Maximum?.Value.Value != null && numValue > dataSchema.Value.Maximum.Value.Value)
                {
                    errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"The specified constant value ({numValue}) cannot be greater than the '{TDDataSchema.MaximumName}' property value ({dataSchema.Value.Maximum.Value.Value}).", constValue.TokenIndex, dataSchema.Value.Maximum.TokenIndex);
                    hasError = true;
                }
            }
            else
            {
                errorReporter.ReportError(ErrorCondition.TypeMismatch, $"The specified constant value must be a number.", constValue.TokenIndex, dataSchema.Value.Type!.TokenIndex);
                hasError = true;
            }

            HashSet<string> supportedProperties = new()
            {
                TDDataSchema.TypeName,
                TDDataSchema.TitleName,
                TDDataSchema.DescriptionName,
                TDDataSchema.MinimumName,
                TDDataSchema.MaximumName,
                TDDataSchema.ConstName,
            };
            if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, null, "a constant number schema", constProperty.TokenIndex))
            {
                hasError = true;
            }

            return !hasError;
        }

        private bool TryValidateIntegerConst<T>(ValueTracker<T> dataSchema, ValueTracker<ObjectHolder> constProperty, ValueTracker<ObjectHolder> constValue)
             where T : TDDataSchema, IDeserializable<T>
        {
            bool hasError = false;

            if (dataSchema.Value.Title != null && !TitleRegex.IsMatch(dataSchema.Value.Title.Value.Value))
            {
                errorReporter.ReportWarning($"Data schema '{TDDataSchema.TitleName}' property value \"{dataSchema.Value.Title.Value.Value}\" does not conform to codegen type naming rules (only alphanumerics, starting with uppercase), which will be problematic unless titles are suppressed via a 'service-desc' linked schema naming file", dataSchema.Value.Title.TokenIndex);
            }

            if (constValue.Value.Value is double numValue && double.IsInteger(numValue))
            {
                if (dataSchema.Value.Minimum?.Value.Value != null && numValue < dataSchema.Value.Minimum.Value.Value)
                {
                    errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"The specified constant value ({numValue}) cannot be less than the '{TDDataSchema.MinimumName}' property value ({dataSchema.Value.Minimum.Value.Value}).", constValue.TokenIndex, dataSchema.Value.Minimum.TokenIndex);
                    hasError = true;
                }
                if (dataSchema.Value.Maximum?.Value.Value != null && numValue > dataSchema.Value.Maximum.Value.Value)
                {
                    errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"The specified constant value ({numValue}) cannot be greater than the '{TDDataSchema.MaximumName}' property value ({dataSchema.Value.Maximum.Value.Value}).", constValue.TokenIndex, dataSchema.Value.Maximum.TokenIndex);
                    hasError = true;
                }
            }
            else
            {
                errorReporter.ReportError(ErrorCondition.TypeMismatch, $"The specified constant value must be an integer.", constValue.TokenIndex, dataSchema.Value.Type!.TokenIndex);
                hasError = true;
            }

            HashSet<string> supportedProperties = new()
            {
                TDDataSchema.TypeName,
                TDDataSchema.TitleName,
                TDDataSchema.DescriptionName,
                TDDataSchema.MinimumName,
                TDDataSchema.MaximumName,
                TDDataSchema.ConstName,
            };
            if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, null, "a constant integer schema", constProperty.TokenIndex))
            {
                hasError = true;
            }

            return !hasError;
        }

        private bool TryValidateBooleanConst<T>(ValueTracker<T> dataSchema, ValueTracker<ObjectHolder> constProperty, ValueTracker<ObjectHolder> constValue)
             where T : TDDataSchema, IDeserializable<T>
        {
            bool hasError = false;

            if (dataSchema.Value.Title != null && !TitleRegex.IsMatch(dataSchema.Value.Title.Value.Value))
            {
                errorReporter.ReportWarning($"Data schema '{TDDataSchema.TitleName}' property value \"{dataSchema.Value.Title.Value.Value}\" does not conform to codegen type naming rules (only alphanumerics, starting with uppercase), which will be problematic unless titles are suppressed via a 'service-desc' linked schema naming file", dataSchema.Value.Title.TokenIndex);
            }

            if (constValue.Value.Value is not bool)
            {
                errorReporter.ReportError(ErrorCondition.TypeMismatch, $"The specified constant value must be Boolean.", constValue.TokenIndex, dataSchema.Value.Type!.TokenIndex);
                hasError = true;
            }

            HashSet<string> supportedProperties = new()
            {
                TDDataSchema.TypeName,
                TDDataSchema.TitleName,
                TDDataSchema.DescriptionName,
                TDDataSchema.ConstName,
            };
            if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, null, "a constant boolean schema", constProperty.TokenIndex))
            {
                hasError = true;
            }

            return !hasError;
        }

        private bool TryValidateResidualProperties(Dictionary<string, long> propertyNames, HashSet<string> supportedProperties, Func<string, bool>? propertyApprover, string schemaDescription, long cfTokenIndex = -1)
        {
            bool hasError = false;

            foreach (KeyValuePair<string, long> propertyName in propertyNames)
            {
                if (propertyApprover?.Invoke(propertyName.Key) != true && !supportedProperties.Contains(propertyName.Key))
                {
                    if (propertyName.Key.Contains(':') && !propertyName.Key.StartsWith($"{AioContextPrefix}:"))
                    {
                        errorReporter.ReportWarning($"Data schema has unrecognized '{propertyName.Key}' property, which will be ignored.", propertyName.Value);
                    }
                    else
                    {
                        errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"Data schema defines {schemaDescription}; property '{propertyName.Key}' is not supported.", propertyName.Value, cfTokenIndex);
                        hasError = true;
                    }
                }
            }

            return !hasError;
        }

        private bool TryValidateObjectDataSchema<T>(ValueTracker<T> dataSchema, DataSchemaKind dataSchemaKind, Func<string, bool>? propertyApprover)
             where T : TDDataSchema, IDeserializable<T>
        {
            if (dataSchema.Value.Properties?.Entries == null && dataSchema.Value.AdditionalProperties == null)
            {
                errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Data schema with '{TDDataSchema.TypeName}' of '{TDValues.TypeObject}' must have either '{TDDataSchema.PropertiesName}' or 'dtv:additionalProperties' property.", dataSchema.TokenIndex);
                return false;
            }

            if (dataSchema.Value.Properties?.Entries != null && dataSchema.Value.AdditionalProperties != null)
            {
                errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Data schema with '{TDDataSchema.TypeName}' of '{TDValues.TypeObject}' cannot have both '{TDDataSchema.PropertiesName}' and 'dtv:additionalProperties' properties.", dataSchema.Value.Properties.TokenIndex, dataSchema.Value.AdditionalProperties.TokenIndex);
                return false;
            }

            bool hasError = false;
            if (dataSchema.Value.Properties?.Entries != null)
            {
                if (dataSchema.Value.Const != null)
                {
                    if (dataSchemaKind != DataSchemaKind.SchemaDefinition)
                    {
                        errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"The '{TDDataSchema.ConstName}' property is permitted only in the first level of a '{TDThing.SchemaDefinitionsName}' element.", dataSchema.Value.Const.TokenIndex);
                        hasError = true;
                    }
                    else if (dataSchema.Value.Const.Value.ValueMap?.Entries == null)
                    {
                        errorReporter.ReportError(ErrorCondition.TypeMismatch, $"The '{TDDataSchema.ConstName}' property value must be an object.", dataSchema.Value.Const.TokenIndex, dataSchema.Value.Type!.TokenIndex);
                        hasError = true;
                    }
                    else
                    {
                        foreach (KeyValuePair<string, ValueTracker<TDDataSchema>> property in dataSchema.Value.Properties.Entries)
                        {
                            if (property.Value.Value.Type == null)
                            {
                                errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Data schema property '{property.Key}' is missing '{TDDataSchema.TypeName}' property, which is required in an object definition that specifies a constant value.", property.Value.TokenIndex, dataSchema.Value.Const.TokenIndex);
                                hasError = true;
                            }
                            else if (property.Value.Value.Const != null)
                            {
                                errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"The '{TDDataSchema.ConstName}' property is not permitted in a schema definition that defines a '{TDDataSchema.ConstName}' value.", property.Value.Value.Const.TokenIndex, dataSchema.Value.Const.TokenIndex);
                                hasError = true;
                            }
                            else if (dataSchema.Value.Const.Value.ValueMap.Entries!.TryGetValue(property.Key, out ValueTracker<ObjectHolder>? constValue))
                            {
                                switch (property.Value.Value.Type!.Value.Value)
                                {
                                    case TDValues.TypeString:
                                        if (!TryValidateStringConst(property.Value, dataSchema.Value.Const, constValue))
                                        {
                                            hasError = true;
                                        }
                                        break;
                                    case TDValues.TypeNumber:
                                        if (!TryValidateNumberConst(property.Value, dataSchema.Value.Const, constValue))
                                        {
                                            hasError = true;
                                        }
                                        break;
                                    case TDValues.TypeInteger:
                                        if (!TryValidateIntegerConst(property.Value, dataSchema.Value.Const, constValue))
                                        {
                                            hasError = true;
                                        }
                                        break;
                                    case TDValues.TypeBoolean:
                                        if (!TryValidateBooleanConst(property.Value, dataSchema.Value.Const, constValue))
                                        {
                                            hasError = true;
                                        }
                                        break;
                                    default:
                                        errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Data schema property '{property.Key}' value must specify a '{TDDataSchema.TypeName}' value of '{TDValues.TypeString}', '{TDValues.TypeNumber}', '{TDValues.TypeInteger}', or '{TDValues.TypeBoolean}' because the object definition specifies a constant value.", property.Value.Value.Type.TokenIndex, dataSchema.Value.Const.TokenIndex);
                                        hasError = true;
                                        break;
                                }
                            }
                            else
                            {
                                errorReporter.ReportError(ErrorCondition.ItemNotFound, $"Data schema '{TDDataSchema.PropertiesName}' value has property named '{property.Key}' that has no value in '{TDDataSchema.ConstName}' elements.", property.Value.TokenIndex, dataSchema.Value.Const.Value.ValueMap.TokenIndex);
                                hasError = true;
                            }
                        }

                        foreach (KeyValuePair<string, ValueTracker<ObjectHolder>> constValue in dataSchema.Value.Const.Value.ValueMap.Entries!)
                        {
                            if (!dataSchema.Value.Properties.Entries.ContainsKey(constValue.Key))
                            {
                                errorReporter.ReportError(ErrorCondition.ItemNotFound, $"Data schema '{TDDataSchema.ConstName}' value has property named '{constValue.Key}' that has no type definition in '{TDDataSchema.PropertiesName}' elements.", constValue.Value.TokenIndex, dataSchema.Value.Properties.TokenIndex);
                                hasError = true;
                            }
                        }
                    }

                    if (dataSchema.Value.Title != null && !TitleRegex.IsMatch(dataSchema.Value.Title.Value.Value))
                    {
                        errorReporter.ReportWarning($"Data schema '{TDDataSchema.TitleName}' property value \"{dataSchema.Value.Title.Value.Value}\" does not conform to codegen type naming rules (only alphanumerics, starting with uppercase), which will be problematic unless titles are suppressed via a 'service-desc' linked schema naming file", dataSchema.Value.Title.TokenIndex);
                    }

                    HashSet<string> supportedProperties = new()
                    {
                        TDDataSchema.TypeName,
                        TDDataSchema.TitleName,
                        TDDataSchema.DescriptionName,
                        TDDataSchema.PropertiesName,
                        TDDataSchema.ConstName,
                    };
                    if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, propertyApprover, "a constant object", dataSchema.Value.Const.TokenIndex))
                    {
                        hasError = true;
                    }
                }
                else
                {
                    foreach (KeyValuePair<string, ValueTracker<TDDataSchema>> property in dataSchema.Value.Properties.Entries)
                    {
                        if (!TryValidateDataSchema(property.Value, null))
                        {
                            hasError = true;
                        }
                    }

                    if (dataSchema.Value.Required?.Elements != null)
                    {
                        foreach (ValueTracker<StringHolder> requiredProperty in dataSchema.Value.Required.Elements)
                        {
                            if (!dataSchema.Value.Properties.Entries.ContainsKey(requiredProperty.Value.Value))
                            {
                                errorReporter.ReportError(ErrorCondition.ItemNotFound, $"Data schema '{TDDataSchema.RequiredName}' property names non-existent property '{requiredProperty.Value.Value}'.", requiredProperty.TokenIndex, dataSchema.Value.Properties.TokenIndex);
                                hasError = true;
                            }
                        }
                    }

                    if (dataSchema.Value.ErrorMessage != null)
                    {
                        if (dataSchemaKind != DataSchemaKind.SchemaDefinition)
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"The '{TDDataSchema.ErrorMessageName}' property is permitted only in the first level of a '{TDThing.SchemaDefinitionsName}' element.", dataSchema.Value.ErrorMessage.TokenIndex);
                            hasError = true;
                        }
                        else if (!dataSchema.Value.Properties.Entries.TryGetValue(dataSchema.Value.ErrorMessage.Value.Value, out ValueTracker<TDDataSchema>? errorMessage))
                        {
                            errorReporter.ReportError(ErrorCondition.ItemNotFound, $"Data schema '{TDDataSchema.ErrorMessageName}' property names non-existent property '{dataSchema.Value.ErrorMessage.Value.Value}'.", dataSchema.Value.ErrorMessage.TokenIndex, dataSchema.Value.Properties.TokenIndex);
                            hasError = true;
                        }
                        else if (errorMessage.Value.Type == null || errorMessage.Value.Type.Value.Value != TDValues.TypeString)
                        {
                            errorReporter.ReportError(ErrorCondition.TypeMismatch, $"Data schema '{TDDataSchema.ErrorMessageName}' property must refer to a property with '{TDDataSchema.TypeName}' of '{TDValues.TypeString}'.", dataSchema.Value.ErrorMessage.TokenIndex, errorMessage.TokenIndex);
                            hasError = true;
                        }
                    }

                    HashSet<string> supportedProperties = new()
                    {
                        TDDataSchema.TypeName,
                        TDDataSchema.TitleName,
                        TDDataSchema.DescriptionName,
                        TDDataSchema.PropertiesName,
                        TDDataSchema.RequiredName,
                        TDDataSchema.ErrorMessageName,
                    };
                    if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, propertyApprover, "a structured object", dataSchema.Value.Properties.TokenIndex))
                    {
                        hasError = true;
                    }
                }

                if (dataSchema.Value.Title != null && !TitleRegex.IsMatch(dataSchema.Value.Title.Value.Value))
                {
                    errorReporter.ReportWarning($"Data schema '{TDDataSchema.TitleName}' property value \"{dataSchema.Value.Title.Value.Value}\" does not conform to codegen type naming rules (only alphanumerics, starting with uppercase), which will be problematic unless titles are suppressed via a 'service-desc' linked schema naming file", dataSchema.Value.Title.TokenIndex);
                }
            }
            else
            {
                if (!TryValidateDataSchema(dataSchema.Value.AdditionalProperties!, null))
                {
                    hasError = true;
                }

                HashSet<string> supportedProperties = new()
                {
                    TDDataSchema.TypeName,
                    TDDataSchema.TitleName,
                    TDDataSchema.DescriptionName,
                    TDDataSchema.AdditionalPropertiesName,
                };
                if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, propertyApprover, "a map", dataSchema.Value.AdditionalProperties!.TokenIndex))
                {
                    hasError = true;
                }
            }

            return !hasError;
        }

        private bool TryValidateArrayDataSchema<T>(ValueTracker<T> dataSchema, Func<string, bool>? propertyApprover)
             where T : TDDataSchema, IDeserializable<T>
        {
            if (dataSchema.Value.Items == null)
            {
                errorReporter.ReportError(ErrorCondition.PropertyMissing, $"Data schema with '{TDDataSchema.TypeName}' of '{TDValues.TypeArray}' must have '{TDDataSchema.ItemsName}' property.", dataSchema.TokenIndex);
                return false;
            }

            bool hasError = false;

            if (!TryValidateDataSchema(dataSchema.Value.Items!, null))
            {
                hasError = true;
            }

            HashSet<string> supportedProperties = new()
            {
                TDDataSchema.TypeName,
                TDDataSchema.TitleName,
                TDDataSchema.DescriptionName,
                TDDataSchema.ItemsName,
            };
            if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, propertyApprover, "an array", dataSchema.Value.Type!.TokenIndex))
            {
                hasError = true;
            }

            return !hasError;
        }

        private bool TryValidateStringDataSchema<T>(ValueTracker<T> dataSchema, DataSchemaKind dataSchemaKind, Func<string, bool>? propertyApprover)
             where T : TDDataSchema, IDeserializable<T>
        {
            bool hasError = false;

            if (dataSchema.Value.Enum?.Elements != null)
            {
                if (dataSchema.Value.Title != null && !TitleRegex.IsMatch(dataSchema.Value.Title.Value.Value))
                {
                    errorReporter.ReportWarning($"Data schema '{TDDataSchema.TitleName}' property value \"{dataSchema.Value.Title.Value.Value}\" does not conform to codegen type naming rules (only alphanumerics, starting with uppercase), which will be problematic unless titles are suppressed via a 'service-desc' linked schema naming file", dataSchema.Value.Title.TokenIndex);
                }

                foreach (ValueTracker<StringHolder> enumValue in dataSchema.Value.Enum.Elements)
                {
                    if (!EnumValueRegex.IsMatch(enumValue.Value.Value))
                    {
                        errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Data schema '{TDDataSchema.EnumName}' property has value \"{enumValue.Value.Value}\" that does not conform to codegen enum member naming rules -- it must start with a letter and contain only alphanumeric characters and underscores", enumValue.TokenIndex);
                        hasError = true;
                    }
                }

                HashSet<string> supportedProperties = new()
                {
                    TDDataSchema.TypeName,
                    TDDataSchema.TitleName,
                    TDDataSchema.DescriptionName,
                    TDDataSchema.EnumName,
                };
                if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, propertyApprover, "an enumerated string", dataSchema.Value.Enum.TokenIndex))
                {
                    hasError = true;
                }
            }
            else
            {
                if (dataSchema.Value.Const != null)
                {
                    if (dataSchemaKind != DataSchemaKind.SchemaDefinition)
                    {
                        errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"The '{TDDataSchema.ConstName}' property is permitted only in the first level of a '{TDThing.SchemaDefinitionsName}' element.", dataSchema.Value.Const.TokenIndex);
                        hasError = true;
                    }
                    else if (!TryValidateStringConst(dataSchema, dataSchema.Value.Const, dataSchema.Value.Const))
                    {
                        hasError = true;
                    }
                }
                else
                {
                    List<string> exclusiveProperties = new();
                    if (dataSchema.Value.Format != null)
                    {
                        exclusiveProperties.Add(TDDataSchema.FormatName);
                    }
                    if (dataSchema.Value.Pattern != null)
                    {
                        exclusiveProperties.Add(TDDataSchema.PatternName);
                    }
                    if (dataSchema.Value.ContentEncoding != null)
                    {
                        exclusiveProperties.Add(TDDataSchema.ContentEncodingName);
                    }

                    if (exclusiveProperties.Count > 1)
                    {
                        errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Data schema string type cannot have more than one of the following properties: {string.Join(", ", exclusiveProperties)}.", dataSchema.TokenIndex);
                        hasError = true;
                    }

                    if (dataSchema.Value.Format != null)
                    {
                        string formatValue = dataSchema.Value.Format.Value.Value;
                        if (formatValue != TDValues.FormatDateTime && formatValue != TDValues.FormatDate && formatValue != TDValues.FormatTime && formatValue != TDValues.FormatUuid)
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"Data schema '{TDDataSchema.FormatName}' property has unsupported value '{formatValue}'; supported values are '{TDValues.FormatDateTime}', '{TDValues.FormatDate}', '{TDValues.FormatTime}', and '{TDValues.FormatUuid}'.", dataSchema.Value.Format.TokenIndex);
                            hasError = true;
                        }
                    }

                    if (dataSchema.Value.ContentEncoding != null)
                    {
                        string contentEncodingValue = dataSchema.Value.ContentEncoding.Value.Value;
                        if (contentEncodingValue != TDValues.ContentEncodingBase64)
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"Data schema '{TDDataSchema.ContentEncodingName}' property has unsupported value '{contentEncodingValue}'; only supported value is '{TDValues.ContentEncodingBase64}'.", dataSchema.Value.ContentEncoding.TokenIndex);
                            hasError = true;
                        }
                    }

                    if (dataSchema.Value.Pattern != null)
                    {
                        string patternValue = dataSchema.Value.Pattern.Value.Value;
                        try
                        {
                            Regex patternRegex = new Regex(patternValue);

                            if (patternRegex.IsMatch(AnArbitraryString))
                            {
                                errorReporter.ReportWarning($"Data schema '{TDDataSchema.PatternName}' property value \"{patternValue}\" matches an arbitrary test string value, so no type restriction will be applied.", dataSchema.Value.Pattern.TokenIndex);
                            }
                            else if (!patternRegex.IsMatch(Iso8601DurationExample) && !patternRegex.IsMatch(DecimalExample))
                            {
                                errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"Data schema '{TDDataSchema.PatternName}' property value \"{patternValue}\" matches neither an ISO 8601 duration value (e.g., \"{Iso8601DurationExample}\") nor a decimal value (e.g., \"{DecimalExample}\"), so indended type is indeterminate.", dataSchema.Value.Pattern.TokenIndex);
                                hasError = true;
                            }
                        }
                        catch (RegexParseException)
                        {
                            errorReporter.ReportError(ErrorCondition.PropertyInvalid, $"Data schema '{TDDataSchema.PatternName}' property has invalid regular expression pattern '{patternValue}'", dataSchema.Value.Pattern.TokenIndex);
                            hasError = true;
                        }
                    }

                    HashSet<string> supportedProperties = new()
                    {
                        TDDataSchema.TypeName,
                        TDDataSchema.TitleName,
                        TDDataSchema.DescriptionName,
                        TDDataSchema.FormatName,
                        TDDataSchema.ContentEncodingName,
                        TDDataSchema.PatternName,
                    };
                    if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, propertyApprover, "a string schema"))
                    {
                        hasError = true;
                    }
                }
            }

            return !hasError;
        }

        private bool TryValidateNumberDataSchema<T>(ValueTracker<T> dataSchema, DataSchemaKind dataSchemaKind, Func<string, bool>? propertyApprover)
             where T : TDDataSchema, IDeserializable<T>
        {
            bool hasError = false;

            if (dataSchema.Value.Minimum?.Value.Value != null && dataSchema.Value.Maximum?.Value.Value != null && dataSchema.Value.Maximum.Value.Value < dataSchema.Value.Minimum.Value.Value)
            {
                errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"The '{TDDataSchema.MaximumName}' property value ({dataSchema.Value.Maximum.Value.Value}) cannot be less than the '{TDDataSchema.MinimumName}' property value ({dataSchema.Value.Minimum.Value.Value}).", dataSchema.Value.Maximum.TokenIndex, dataSchema.Value.Minimum.TokenIndex);
                hasError = true;
            }

            if (dataSchema.Value.Const != null)
            {
                if (dataSchemaKind != DataSchemaKind.SchemaDefinition)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"The '{TDDataSchema.ConstName}' property is permitted only in the first level of a '{TDThing.SchemaDefinitionsName}' element.", dataSchema.Value.Const.TokenIndex);
                    hasError = true;
                }
                else if (!TryValidateNumberConst(dataSchema, dataSchema.Value.Const, dataSchema.Value.Const))
                {
                    hasError = true;
                }
            }
            else
            {
                HashSet<string> supportedProperties = new()
                {
                    TDDataSchema.TypeName,
                    TDDataSchema.TitleName,
                    TDDataSchema.DescriptionName,
                    TDDataSchema.MinimumName,
                    TDDataSchema.MaximumName,
                };
                if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, propertyApprover, "a number schema"))
                {
                    hasError = true;
                }
            }

            return !hasError;
        }

        private bool TryValidateIntegerDataSchema<T>(ValueTracker<T> dataSchema, DataSchemaKind dataSchemaKind, Func<string, bool>? propertyApprover)
             where T : TDDataSchema, IDeserializable<T>
        {
            bool hasError = false;

            if (dataSchema.Value.Minimum?.Value.Value != null && !double.IsInteger(dataSchema.Value.Minimum.Value.Value))
            {
                errorReporter.ReportError(ErrorCondition.TypeMismatch, $"The '{TDDataSchema.MinimumName}' property value must be an integer.", dataSchema.Value.Minimum.TokenIndex);
                hasError = true;
            }
            if (dataSchema.Value.Maximum?.Value.Value != null && !double.IsInteger(dataSchema.Value.Maximum.Value.Value))
            {
                errorReporter.ReportError(ErrorCondition.TypeMismatch, $"The '{TDDataSchema.MaximumName}' property value must be an integer.", dataSchema.Value.Maximum.TokenIndex);
                hasError = true;
            }

            if (hasError)
            {
                return false;
            }

            if (dataSchema.Value.Minimum?.Value.Value != null && dataSchema.Value.Maximum?.Value.Value != null && dataSchema.Value.Maximum.Value.Value < dataSchema.Value.Minimum.Value.Value)
            {
                errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"The '{TDDataSchema.MaximumName}' property value ({dataSchema.Value.Maximum.Value.Value}) cannot be less than the '{TDDataSchema.MinimumName}' property value ({dataSchema.Value.Minimum.Value.Value}).", dataSchema.Value.Maximum.TokenIndex, dataSchema.Value.Minimum.TokenIndex);
                hasError = true;
            }

            if (dataSchema.Value.Const != null)
            {
                if (dataSchemaKind != DataSchemaKind.SchemaDefinition)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"The '{TDDataSchema.ConstName}' property is permitted only in the first level of a '{TDThing.SchemaDefinitionsName}' element.", dataSchema.Value.Const.TokenIndex);
                    hasError = true;
                }
                else if (dataSchema.Value.Const != null && !TryValidateIntegerConst(dataSchema, dataSchema.Value.Const, dataSchema.Value.Const))
                {
                    hasError = true;
                }
            }
            else
            {
                HashSet<string> supportedProperties = new()
                {
                    TDDataSchema.TypeName,
                    TDDataSchema.TitleName,
                    TDDataSchema.DescriptionName,
                    TDDataSchema.MinimumName,
                    TDDataSchema.MaximumName,
                };
                if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, propertyApprover, "an integer schema"))
                {
                    hasError = true;
                }
            }

            return !hasError;
        }

        private bool TryValidateBooleanDataSchema<T>(ValueTracker<T> dataSchema, DataSchemaKind dataSchemaKind, Func<string, bool>? propertyApprover)
             where T : TDDataSchema, IDeserializable<T>
        {
            bool hasError = false;

            if (dataSchema.Value.Const != null)
            {
                if (dataSchemaKind != DataSchemaKind.SchemaDefinition)
                {
                    errorReporter.ReportError(ErrorCondition.PropertyUnsupported, $"The '{TDDataSchema.ConstName}' property is permitted only in the first level of a '{TDThing.SchemaDefinitionsName}' element.", dataSchema.Value.Const.TokenIndex);
                    hasError = true;
                }
                else if (!TryValidateBooleanConst(dataSchema, dataSchema.Value.Const, dataSchema.Value.Const))
                {
                    hasError = true;
                }
            }
            else
            {
                HashSet<string> supportedProperties = new()
                {
                    TDDataSchema.TypeName,
                    TDDataSchema.TitleName,
                    TDDataSchema.DescriptionName,
                };
                if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, propertyApprover, "a Boolean schema"))
                {
                    hasError = true;
                }
            }

            return !hasError;
        }

        private bool TryValidateNullDataSchema<T>(ValueTracker<T> dataSchema, DataSchemaKind dataSchemaKind, Func<string, bool>? propertyApprover, bool contentTypeIsRawOrCustom, long contentTypeTokenIndex)
             where T : TDDataSchema, IDeserializable<T>
        {
            if (dataSchemaKind != DataSchemaKind.Action && dataSchemaKind != DataSchemaKind.Event)
            {
                errorReporter.ReportError(ErrorCondition.PropertyUnsupportedValue, $"A '{TDDataSchema.TypeName}' property value of '{TDValues.TypeNull}' is permitted only in the '{TDAction.InputName}' or '{TDAction.OutputName}' property value of an '{TDThing.ActionsName}' element or in the '{TDEvent.DataName}' property value of an '{TDThing.EventsName}' element.", dataSchema.Value.Type!.TokenIndex);
                return false;
            }
            else if (!contentTypeIsRawOrCustom)
            {
                errorReporter.ReportError(ErrorCondition.ValuesInconsistent, $"Data schema with '{TDDataSchema.TypeName}' of '{TDValues.TypeNull}' is permitted only in an affordance with a form that specifies '{TDForm.ContentTypeName}' of '{TDValues.ContentTypeRaw}' or '{TDValues.ContentTypeCustom}'.", dataSchema.Value.Type!.TokenIndex, contentTypeTokenIndex);
                return false;
            }

            HashSet<string> supportedProperties = new()
            {
                TDDataSchema.TypeName,
                TDDataSchema.TitleName,
                TDDataSchema.DescriptionName,
            };
            if (!TryValidateResidualProperties(dataSchema.Value.PropertyNames, supportedProperties, propertyApprover, "a null schema"))
            {
                return false;
            }

            return true;
        }

        private enum DataSchemaKind
        {
            Undistinguished,
            Action,
            Property,
            Event,
            SchemaDefinition,
        }

        private enum FormsKind
        {
            Root,
            Action,
            Property,
            Event,
        }
    }
}
