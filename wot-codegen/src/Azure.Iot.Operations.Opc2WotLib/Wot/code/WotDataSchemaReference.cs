// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    public class WotDataSchemaReference : WotDataSchema
    {
        private readonly string schemaName;

        public WotDataSchemaReference(string schemaName)
        {
            this.schemaName = schemaName;
        }

        public override string TransformText()
        {
            return $"\"tm:ref\": \"#/schemaDefinitions/{schemaName}\"";
        }
    }
}
