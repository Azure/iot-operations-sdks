using Azure.Iot.Operations.Services.LeasedLock;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Mqtt.Session;

namespace LeasedLockSample;

internal sealed class Program
{
    private const string _sharedResourceKey = "someSharedResourceKey";

    private static void Main()
    {
        using CancellationTokenSource cts = new CancellationTokenSource();

        Task.Run(
            () =>
            {
                do
                {
                    while (!Console.KeyAvailable)
                    {
                        // wait for user to press "escape"
                    }
                } while (Console.ReadKey(true).Key != ConsoleKey.Escape);

                cts.Cancel();
            }
        );

        RunSampleAsync(cts.Token).Wait();
    }

    private static async Task RunSampleAsync(CancellationToken cancellationToken)
    {
        await using MqttSessionClient mqttClient = new MqttSessionClient();
        await mqttClient.ConnectAsync(new MqttConnectionSettings("localhost") { TcpPort = 1883, UseTls = false, ClientId = "someClientId" });

        await using LeasedLockClient leasedLockClient = new LeasedLockClient(mqttClient, "someLock");
        await using StateStoreClient stateStoreClient = new StateStoreClient(mqttClient);

        bool sharedResourceChanged = false;
        while (!sharedResourceChanged)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeSpan leaseDuration = TimeSpan.FromMinutes(1);
            AcquireLockResponse response =
                await leasedLockClient.AcquireLockAsync(
                    leaseDuration, 
                    null, 
                    cancellationToken).ConfigureAwait(false);

            HybridLogicalClock fencingToken = response.FencingToken!;

            Console.WriteLine("Successfully acquired lock. Now altering a shared resource in the State Store");

            string newValue = Guid.NewGuid().ToString();
            StateStoreSetResponse setResponse = await stateStoreClient.SetAsync(
                _sharedResourceKey,
                newValue,
                new StateStoreSetRequestOptions()
                {
                    // The fencing token returned by the service must be used in set request to ensure that
                    // the service only executes the request if the lock was acquired.
                    FencingToken = fencingToken,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            sharedResourceChanged = setResponse.Success;

            if (sharedResourceChanged)
            {
                Console.WriteLine($"Successfully changed the value of the shared resource " +
                    $"{_sharedResourceKey} to value {newValue}");
            }
            else
            {
                Console.WriteLine($"Failed to change the value of the shared resource " +
                    $"{_sharedResourceKey} to value {newValue}. Will attempt" +
                    $"to re-acquire the lock before trying again.");
            }
        }

        // nothing more to do while owning the lock, so release it
        await leasedLockClient.ReleaseLockAsync(null, cancellationToken).ConfigureAwait(false);
    }
}