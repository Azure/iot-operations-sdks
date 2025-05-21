namespace Remapper
{
    using System;
    using System.IO;
    using Newtonsoft.Json.Linq;
    using Common;

    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: Remapper alpha|beta");
                return;
            }

            string dataSource = args[0];

            string deviceAddress;
            string commFormat;
            string deviceType;

            switch (dataSource)
            {
                case "alpha":
                    deviceAddress = Constants.AlphaDeviceAddress;
                    commFormat = Constants.CommFormatCsv;
                    deviceType = Constants.AlphaDeviceType;
                    break;
                case "beta":
                    deviceAddress = Constants.BetaDeviceAddress;
                    commFormat = Constants.CommFormatJson;
                    deviceType = Constants.BetaDeviceType;
                    break;
                default:
                    Console.WriteLine("Only data sources 'alpha' and 'beta' are recognized");
                    return;
            }

            Console.WriteLine($"Setting mapping for {Constants.MqttNamespace} to device address {deviceAddress} with type ID {deviceType} sending {commFormat} format");

            JObject mapping;
            using (StreamReader reader = File.OpenText(Constants.MappingFilePath))
            {
                mapping = JObject.Parse(reader.ReadToEnd());
            }

            JObject addressAndType = new JObject
            {
                new JProperty(Constants.MappingPropertyDeviceAddress, deviceAddress),
                new JProperty(Constants.MappingPropertyCommFormat, commFormat),
                new JProperty(Constants.MappingPropertyDeviceTypeId, deviceType),
            };

            JObject update = new JObject() { new JProperty(Constants.MqttNamespace, addressAndType) };

            mapping.Merge(update);

            File.WriteAllText(Constants.MappingFilePath, mapping.ToString());
        }
    }
}
