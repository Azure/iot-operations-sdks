// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace ProtocolInterop.Demo
{
    internal sealed class CounterCollectionClient : CounterGroup.Client
    {
        public CounterCollectionClient()
        {
        }
    }

    internal sealed class Program
    {
        private enum CounterCommand
        {
            Increment,
            GetLocation
        }

        const string clientId = "DotnetCounterClient";

        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: CmdClient {INC|GET} counter_name");
                return;
            }

            CounterCommand command = args[0].ToLowerInvariant() switch
            {
                "inc" => CounterCommand.Increment,
                "get" => throw new ArgumentException("command GET not supported"),
                _ => throw new ArgumentException("command must be INC or GET")
            };

            string counterName = args[1];

            CounterCollectionClient client = new ();

            try
            {
                switch (command)
                {
                    case CounterCommand.Increment:
                        IncrementResponsePayload incResponse = await client.IncrementAsync(new IncrementRequestPayload { CounterName = counterName });
                        Console.WriteLine($"New value = {incResponse.CounterValue}");
                        break;
                    case CounterCommand.GetLocation:
                        break;
                }
            }
            catch (CounterErrorException counterException)
            {
                Console.WriteLine($"Request failed with exception: '{counterException.Message}'");

                switch (counterException.CounterError.Condition)
                {
                    case ConditionSchema.CounterNotFound:
                        Console.WriteLine($"Counter '{counterName}' was not found");
                        break;
                    case ConditionSchema.CounterOverflow:
                        Console.WriteLine($"Counter '{counterName}' has overflowed");
                        break;
                }
            }
        }
    }
}
