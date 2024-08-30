namespace Akri.Dtdl.Codegen.IntegrationTests.STK
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MQTTnet;
    using MQTTnet.Client;
    using MQTTnet.Packets;

    public class MqttEmulator
    {
        private const string SharePrefix = "$share/";
        private static readonly int GroupIdStart = SharePrefix.Length;

        private Dictionary<string, EmulatedClient> clients;
        private Dictionary<string, List<string>> subscriptions;
        private Dictionary<string, Dictionary<string, List<string>>> sharedSubscriptions;
        private Dictionary<string, int> shareSelectors;

        public MqttEmulator()
        {
            clients = new();
            subscriptions = new();
            sharedSubscriptions = new();
            shareSelectors = new();
        }

        public EmulatedClient GetClient(string clientId)
        {
            if (clients.TryGetValue(clientId, out var client))
            {
                return client;
            }
            else
            {
                client = new EmulatedClient(clientId, this);
                clients[clientId] = client;
                return client;
            }
        }

        public async Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken)
        {
            HashSet<string> clientIds = new();

            foreach (KeyValuePair<string, List<string>> subscription in subscriptions)
            {
                if (MqttTopicFilterComparer.Compare(applicationMessage.Topic, subscription.Key) == MqttTopicFilterCompareResult.IsMatch)
                {
                    foreach (string clientId in subscription.Value)
                    {
                        clientIds.Add(clientId);
                    }
                }
            }

            foreach (KeyValuePair<string, Dictionary<string, List<string>>> sharedSubscription in sharedSubscriptions)
            {
                if (MqttTopicFilterComparer.Compare(applicationMessage.Topic, sharedSubscription.Key) == MqttTopicFilterCompareResult.IsMatch)
                {
                    foreach (KeyValuePair<string, List<string>> shareGroup in sharedSubscription.Value)
                    {
                        string selectedClientId = SelectClientIdForShareGroup(shareGroup.Key, shareGroup.Value);
                        clientIds.Add(selectedClientId);
                    }
                }
            }

            foreach (string clientId in clientIds)
            {
                await clients[clientId].ReceiveAsync(applicationMessage, cancellationToken).ConfigureAwait(false);
            }

            return new MqttClientPublishResult(1, clientIds.Any() ? MqttClientPublishReasonCode.Success : MqttClientPublishReasonCode.NoMatchingSubscribers, "mock", null);
        }

        public void Subscribe(string clientId, List<MqttTopicFilter> TopicFilters)
        {
            foreach (MqttTopicFilter topicFilter in TopicFilters)
            {
                (string? groupId, string topic) = GetGroupIdAndTopic(topicFilter.Topic);

                if (groupId != null)
                {
                    if (!sharedSubscriptions.TryGetValue(topic, out Dictionary<string, List<string>>? sharedSubscription))
                    {
                        sharedSubscription = new();
                        sharedSubscriptions[topic] = sharedSubscription;
                    }

                    AddClientIdToCollection(clientId, sharedSubscription, groupId);
                }
                else
                {
                    AddClientIdToCollection(clientId, subscriptions, topic);
                }
            }
        }

        private void AddClientIdToCollection(string clientId, Dictionary<string, List<string>> collection, string collectionKey)
        {
            if (!collection.TryGetValue(collectionKey, out List<string>? collectedClientIds))
            {
                collectedClientIds = new();
                collection[collectionKey] = collectedClientIds;
            }

            collectedClientIds.Add(clientId);
        }

        private (string?, string) GetGroupIdAndTopic(string filterTopic)
        {
            if (filterTopic.StartsWith(SharePrefix))
            {
                int sepIx = filterTopic.IndexOf('/', GroupIdStart);
                string groupId = filterTopic.Substring(GroupIdStart, sepIx - GroupIdStart);
                string topic = filterTopic.Substring(sepIx + 1);
                return (groupId, topic);
            }
            else
            {
                return (null, filterTopic);
            }
        }

        private string SelectClientIdForShareGroup(string groupId, List<string> clientIds)
        {
            if (!shareSelectors.TryGetValue(groupId, out int shareIndex))
            {
                shareIndex = 0;
            }

            shareSelectors[groupId] = shareIndex + 1;

            return clientIds[shareIndex % clientIds.Count];
        }
    }
}
