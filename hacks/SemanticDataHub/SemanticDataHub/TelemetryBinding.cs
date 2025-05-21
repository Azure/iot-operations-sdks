namespace SemanticDataHub
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class TelemetryBinding
    {
        private Dictionary<string, IDataTransformer> dataTransformers;

        public TelemetryBinding(string bindingFilePath)
        {
            dataTransformers = new Dictionary<string, IDataTransformer>();

            using (StreamReader reader = File.OpenText(bindingFilePath))
            {
                using (JsonDocument bindingDoc = JsonDocument.Parse(reader.ReadToEnd()))
                {
                    DeviceTypeId = bindingDoc.RootElement.GetProperty("deviceTypeId").GetString()!;
                    ModelId = bindingDoc.RootElement.GetProperty("modelId").GetString()!;

                    JsonElement telemElt = bindingDoc.RootElement.GetProperty("Telemetry");
                    foreach (JsonProperty telemProp in telemElt.EnumerateObject())
                    {
                        dataTransformers[telemProp.Name] = DataTransformerFactory.Create(telemProp.Name, telemProp.Value, Path.GetFileName(bindingFilePath));
                    }
                }
            }
        }

        public string DeviceTypeId { get; }

        public string ModelId { get; }

        public string? GetTelemetry(string telemName, JToken sourceToken)
        {
            JArray sourceArray = new JArray(sourceToken);
            JToken? resultToken = dataTransformers[telemName].TransformData(sourceArray);
            return resultToken != null ? new JObject { new JProperty(telemName, resultToken) }.ToString(Formatting.None) : null;
        }
    }
}
