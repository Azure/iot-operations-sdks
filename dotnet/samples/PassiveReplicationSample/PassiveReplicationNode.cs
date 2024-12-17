using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;
using System.Diagnostics;

namespace Azure.Iot.Operations.Services.PassiveReplicationSample
{
    public class PassiveReplicationNode : BackgroundService, IAsyncDisposable
    {
        private readonly StateStoreKey SharedResourceKeyToUpdate = new("someKeyToUpdate");
        private static TimeSpan _electionTerm = TimeSpan.FromSeconds(1);
        private string? _lastKnownLeader;

        private readonly ILogger<PassiveReplicationNode> _logger;
        private readonly IConfiguration _configuration;
        private readonly MqttSessionClient _mqttClient;
        private readonly StateStoreClient _stateStoreClient;
        private readonly LeaderElectionClient _leaderElectionClient;

        public PassiveReplicationNode(ILogger<PassiveReplicationNode> logger, IConfiguration config, MqttSessionClient mqttClient, StateStoreClient stateStoreClient, LeaderElectionClient leaderElectionClient)
        {
            _logger = logger;
            _configuration = config;
            _mqttClient = mqttClient;
            _stateStoreClient = stateStoreClient;
            _leaderElectionClient = leaderElectionClient;
        }

        public async ValueTask DisposeAsync()
        {
            await _stateStoreClient.DisposeAsync();
            await _leaderElectionClient.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            string? mqttConnectionString = _configuration.GetConnectionString("PassiveReplicationNode");
            if (string.IsNullOrWhiteSpace(mqttConnectionString))
            {
                throw new Exception("No connection string set");
            }

            _logger.LogInformation("Found the connection string: {0}", mqttConnectionString);

            if (!mqttConnectionString.Contains("ClientId="))
            {
                mqttConnectionString += ";ClientId=" + _leaderElectionClient.CandidateName;
            }

            MqttClientConnectResult connAck;
            try
            {
                connAck = await _mqttClient.ConnectAsync(MqttConnectionSettings.FromConnectionString(mqttConnectionString));
            }
            catch (Exception e)
            {
                throw new Exception("Failed to connect over MQTT", e);
            }

            _logger.LogInformation("Connected to MQTT server with result {0}", connAck.ResultCode);

            _leaderElectionClient.AutomaticRenewalOptions = new LeaderElectionAutomaticRenewalOptions()
            {
                AutomaticRenewal = true,
                ElectionTerm = _electionTerm,
                RenewalPeriod = _electionTerm.Subtract(TimeSpan.FromMilliseconds(300)),
            };

            _leaderElectionClient.LeadershipChangeEventReceivedAsync += (sender, args) =>
            {
                if (args.NewState == LeadershipPositionState.LeaderElected
                    && !args.NewLeader!.Equals(_leaderElectionClient.CandidateName)
                    && !args.NewLeader!.Equals(_lastKnownLeader))
                {
                    _logger.LogInformation("This node was alerted that node {0} was elected leader. Current timestamp: {1}", args.NewLeader!.GetString(), new HybridLogicalClock());
                    _lastKnownLeader = args.NewLeader!.GetString();
                }
                else if (args.NewState == LeadershipPositionState.NoLeader)
                {
                    _logger.LogInformation("Node {0} was alerted that it is no longer the leader. Current timestamp: {1}", _leaderElectionClient.CandidateName, new HybridLogicalClock());
                }

                return Task.CompletedTask;
            };

            await _leaderElectionClient.ObserveLeadershipChangesAsync(cancellationToken: cancellationToken);

            // Delay the initial campaign attempt to prevent herding effects from large
            // numbers of pods being deployed at the same time all campaigning at the
            // same time.
            await Task.Delay(TimeSpan.FromSeconds(new Random().Next(2, 8)));

            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Node {0} is now waiting to be elected leader. Current timestamp: {1}", _leaderElectionClient.CandidateName, new HybridLogicalClock());

                // Note that this function awaits being elected leader. This may take a several election cycles,
                // but the worker node isn't doing anything in the meantime.
                //
                // Also note that the return value is ignored here but is viewed in the loop by checking _leaderElectionClient.PreviousCampaignResult
                CampaignResponse campaignResponse = await _leaderElectionClient.CampaignAsync(
                    _electionTerm,
                    null,
                    cancellationToken);

                _logger.LogInformation("Node {0} was elected leader. Current timestamp: {1} Fencing token: {2}", _leaderElectionClient.CandidateName, new HybridLogicalClock(), campaignResponse.FencingToken);

                try
                {
                    // This function runs until either the node is no longer a leader or until the provided cancellation
                    // token is canceled.
                    await DoLeaderThingsAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    await _leaderElectionClient.ResignAsync();
                    throw;
                }
            }

            await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptions() { ReasonString = "command end" }, cancellationToken);
            Environment.Exit(0);
        }

        private async Task DoLeaderThingsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HybridLogicalClock? fencingToken = _leaderElectionClient.LastKnownCampaignResult?.FencingToken;

                if (fencingToken == null)
                {
                    // likely because the fencing token used was out-of-date
                    _logger.LogInformation("Node {0} no longer has access to a valid fencing token. This node will stop acting as a leader" +
                        " and go back to campaigning to be leader.", _leaderElectionClient.CandidateName);
                    return;
                }

                // As leader, you can attempt to alter shared resources in the State Store. This may still fail though
                // if the fencing token becomes out-of-date. For example, this thread may stall for a day, wake up,
                // and try to alter a shared resource. By that point, another node will have acquired the lock and
                // a newer fencing token will have been created.
                StateStoreSetResponse setResponse = await _stateStoreClient.SetAsync(
                    SharedResourceKeyToUpdate,
                    new StateStoreValue(Guid.NewGuid().ToString()),
                    new StateStoreSetRequestOptions()
                    {
                        // This fencing token value is automatically updated as more elections are run. It may become
                        // out-of-date though and this operation will fail if this is the case.
                        FencingToken = fencingToken,
                    },
                    cancellationToken: cancellationToken);

                if (setResponse.Success)
                {
                    // shared resource was successfully updated. Update the locally held version of that key.
                    _logger.LogInformation("Node {0} successfully altered the shared resource as the leader. Current timestamp: {1} Fencing token: {2}", _leaderElectionClient.CandidateName, new HybridLogicalClock(), fencingToken);
                }
                else
                {
                    // likely because the fencing token used was out-of-date
                    _logger.LogInformation("Node {0} failed to alter a shared resource using fencing token {1}. This node will stop acting as a leader" +
                        " and go back to campaigning to be leader.", _leaderElectionClient.CandidateName, fencingToken);
                    return;
                }

                // Wait some time before altering the value again
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }
}
