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
            List<TDThing>? things = TDParser.Parse(tdJson);
            if (things != null)
            {
                TDThing? thing = things.First();
                if (thing.Context?.Elements != null)
                {
                    foreach (var context in thing.Context.Elements)
                    {
                        Console.WriteLine($"@context: {context}");
                    }
                }
                Console.WriteLine($"ID: {thing.Id}");
                Console.WriteLine($"Title: {thing.Title}");
                if (thing.Forms?.Elements != null)
                {
                    foreach (var form in thing.Forms.Elements)
                    {
                        Console.WriteLine($"Form href: {form.Value.Href}");
                        if (form.Value.ContentType != null)
                        {
                            Console.WriteLine($"  ContentType: {form.Value.ContentType}");
                        }
                        if (form.Value.Topic != null)
                        {
                            Console.WriteLine($"  Topic: {form.Value.Topic}");
                        }
                        if (form.Value.Op != null)
                        {
                            Console.WriteLine($"  Op: {string.Join(", ", form.Value.Op)}");
                        }
                    }
                }
                if (thing.Events?.Entries != null)
                {
                    foreach (var evt in thing.Events.Entries)
                    {
                        Console.WriteLine($"Event: {evt.Key}");
                    }
                }
                if (thing.SchemaDefinitions?.Entries != null)
                {
                    foreach (var schema in thing.SchemaDefinitions.Entries)
                    {
                        Console.WriteLine($"SchemaDefinition: {schema.Key}");
                        if (schema.Value.Value.AdditionalProperties != null)
                        {
                            Console.WriteLine($"  AdditionalProperties: {schema.Value.Value.AdditionalProperties}");
                        }
                    }
                }
                if (thing.Properties?.Entries != null)
                {
                    foreach (var prop in thing.Properties.Entries)
                    {
                        Console.WriteLine($"Property: {prop.Key}");
                        if (prop.Value.Value.AdditionalProperties != null)
                        {
                            Console.WriteLine($"  AdditionalProperties: {prop.Value.Value.AdditionalProperties}");
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
