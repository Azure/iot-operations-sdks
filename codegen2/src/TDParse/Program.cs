namespace TDParse
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: TDParse <path to Thing Description JSON file>");
                return;
            }

            FileInfo file = new FileInfo(args[0]);
            if (!file.Exists)
            {
                Console.WriteLine($"File not found: {file.FullName}");
                return;
            }

            string tdJson = File.ReadAllText(file.FullName);
            List<TDThing>? things = TDParser.ParseMultiple(tdJson);
            if (things != null)
            {
                TDThing? thing = things.First();
                if (thing.Context != null)
                {
                    foreach (var context in thing.Context)
                    {
                        Console.WriteLine($"@context: {context}");
                    }
                }
                Console.WriteLine($"ID: {thing.Id}");
                Console.WriteLine($"Title: {thing.Title}");
                if (thing.Forms != null)
                {
                    foreach (var form in thing.Forms)
                    {
                        Console.WriteLine($"Form href: {form.Href}");
                        if (form.ContentType != null)
                        {
                            Console.WriteLine($"  ContentType: {form.ContentType}");
                        }
                        if (form.Topic != null)
                        {
                            Console.WriteLine($"  Topic: {form.Topic}");
                        }
                        if (form.Op != null)
                        {
                            Console.WriteLine($"  Op: {string.Join(", ", form.Op)}");
                        }
                    }
                }
                if (thing.Events != null)
                {
                    foreach (var evt in thing.Events)
                    {
                        Console.WriteLine($"Event: {evt.Key}");
                    }
                }
                if (thing.SchemaDefinitions != null)
                {
                    foreach (var schema in thing.SchemaDefinitions)
                    {
                        Console.WriteLine($"SchemaDefinition: {schema.Key}");
                        if (schema.Value.AdditionalProperties != null)
                        {
                            Console.WriteLine($"  AdditionalProperties: {schema.Value.AdditionalProperties}");
                        }
                    }
                }
                if (thing.Properties != null)
                {
                    foreach (var prop in thing.Properties)
                    {
                        Console.WriteLine($"Property: {prop.Key}");
                        if (prop.Value.AdditionalProperties != null)
                        {
                            Console.WriteLine($"  AdditionalProperties: {prop.Value.AdditionalProperties}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Failed to parse the Thing Description.");
            }
        }
    }
}
