namespace DataSourceBeta
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Common;

    internal class Program
    {
        const double surfBaseTemp = 66.6;
        const double surfTempVar = 1.5;
        const double intBaseTemp = 55.5;
        const double intTempVar = 1.5;
        const double envBaseTemp = 33.3;
        const double envTempVar = 1.5;

        const double basePressure = 8.0;
        const double pressureVar = 4.0;

        const double posMagnitude = 6.0;
        const double posAngleInc = 0.5;

        static readonly TimeSpan measDelay = TimeSpan.FromSeconds(0);
        static readonly TimeSpan measInterval = TimeSpan.FromSeconds(4);

        static readonly TimeSpan ctrlDelay = TimeSpan.FromSeconds(2);
        static readonly TimeSpan ctrlInterval = TimeSpan.FromSeconds(8);

        static async Task SendMeasurement(StreamWriter pipeWriter)
        {
            await Task.Delay(measDelay);

            Random rand = new();
            while (true)
            {
                try
                {
                    double surfTemp = surfBaseTemp + rand.NextDouble() * surfTempVar;
                    double intTemp = intBaseTemp + rand.NextDouble() * intTempVar;
                    double envTemp = envBaseTemp + rand.NextDouble() * envTempVar;
                    double pressure = basePressure + rand.NextDouble() * pressureVar;

                    JObject intObj = new ();
                    intObj.Add("Temperature", new JValue(intTemp));
                    intObj.Add("Pressure", new JValue(pressure));

                    JObject surfObj = new ();
                    surfObj.Add("Temperature", new JValue(surfTemp));
                    surfObj.Add("Extension", new JValue(0.01));

                    JObject equipObj = new ();
                    equipObj.Add("Internal", intObj);
                    equipObj.Add("Surface", surfObj);
                    equipObj.Add("Status", new JValue("Good"));

                    JObject envObj = new();
                    envObj.Add("Temperature", new JValue(envTemp));

                    JObject measObj = new();
                    measObj.Add("Equipment", equipObj);
                    measObj.Add("Environment", envObj);

                    JObject dataObj = new JObject();
                    dataObj.Add("Measurement", measObj);

                    string data = dataObj.ToString(Formatting.None);

                    Console.WriteLine(data);
                    pipeWriter.WriteLine(data);
                }
                catch (IOException)
                {
                    return;
                }

                await Task.Delay(measInterval);
            }
        }

        static async Task SendControl(StreamWriter pipeWriter)
        {
            await Task.Delay(ctrlDelay);

            double angle = 0.0;

            Random rand = new();
            while (true)
            {
                angle += posAngleInc;

                try
                {
                    double x = posMagnitude / Math.Cos(angle);
                    double y = posMagnitude / Math.Sin(angle);
                    string mode = rand.NextDouble() < 0.1 ? "calibrate" : "scan";

                    JObject posObj = new();
                    posObj.Add("x", new JValue(x));
                    posObj.Add("y", new JValue(y));

                    JObject ctrlObj = new();
                    ctrlObj.Add("Mode", new JValue(mode));
                    ctrlObj.Add("Position", posObj);

                    JObject dataObj = new JObject();
                    dataObj.Add("Control", ctrlObj);

                    string data = dataObj.ToString(Formatting.None);

                    Console.WriteLine(data);
                    pipeWriter.WriteLine(data);
                }
                catch (IOException)
                {
                    return;
                }

                await Task.Delay(ctrlInterval);
            }
        }

        static async Task Main(string[] args)
        {
            while (true)
            {
                using (NamedPipeServerStream pipeServer = new NamedPipeServerStream(Constants.BetaDeviceAddress, PipeDirection.Out))
                {
                    Console.Write($"Waiting for client connection to named pipe '{Constants.BetaDeviceAddress}' ...");
                    pipeServer.WaitForConnection();
                    Console.WriteLine(" Client connected.");

                    try
                    {
                        using (StreamWriter pipeWriter = new StreamWriter(pipeServer))
                        {
                            pipeWriter.AutoFlush = true;

                            Task sendTempTask = Task.Run(async () => await SendMeasurement(pipeWriter));
                            Task sendPosTask = Task.Run(async () => await SendControl(pipeWriter));

                            await Task.WhenAll(new Task[] { sendTempTask, sendPosTask });
                        }
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Client disconnected.");
                    }
                }
            }
        }
    }
}
