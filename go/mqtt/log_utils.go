// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"fmt"
	"log/slog"

	"github.com/eclipse/paho.golang/paho"
)

func (c *SessionClient) debug(msg string, args ...any) {
	if c.debugMode {
		msg = fmt.Sprintf("[%s][%s]", c.ClientID(), msg)
		c.logger.Debug(msg, args...)
	}
}

func (c *SessionClient) info(msg string, args ...any) {
	if c.debugMode {
		msg = fmt.Sprintf(" [%s][%s]", c.ClientID(), msg)
		c.logger.Info(msg, args...)
	}
}

func (c *SessionClient) error(msg string, args ...any) {
	msg = fmt.Sprintf("[%s][%s]", c.ClientID(), msg)
	c.logger.Error(msg, args...)
}

func (c *SessionClient) logPacket(packet any) {
	switch p := packet.(type) {
	case *paho.Subscribe:
		c.logSubscribe(p)
	case *paho.Unsubscribe:
		c.logUnsubscribe(p)
	case *paho.Publish:
		c.logPublish(p)
	default:
		c.info("unknown packet", slog.Any("Packet", p))
	}
}

func (c *SessionClient) logConnect(connect *paho.Connect) {
	if connect == nil {
		return
	}

	c.info("send connect",
		slog.String("ClientID", connect.ClientID),
		slog.String("Username", connect.Username),
		slog.Bool("UsernameFlag", connect.UsernameFlag),
		slog.Bool("PasswordFlag", connect.PasswordFlag),
		slog.Int("KeepAlive", int(connect.KeepAlive)),
		slog.Bool("CleanStart", connect.CleanStart),
		slog.Any("Properties", connect.Properties),
		slog.Any("WillMessage", connect.WillMessage),
		slog.Any("WillProperties ", connect.WillProperties),
		slog.Any("Properties", connect.Properties))
}

func (c *SessionClient) logDisconnect(disconnect *paho.Disconnect) {
	if disconnect == nil {
		return
	}

	c.info("send disconnect",
		slog.String("ReasonCode", string(disconnect.ReasonCode)),
		slog.Any("Properties", disconnect.Properties))
}

func (c *SessionClient) logSubscribe(sub *paho.Subscribe) {
	if sub == nil {
		return
	}

	subscriptionGroups := make([]any, 0, len(sub.Subscriptions))
	for _, subOpt := range sub.Subscriptions {
		subscriptionGroups = append(subscriptionGroups,
			slog.Group("Subscription",
				slog.String("Topic", subOpt.Topic),
				slog.Int("QoS", int(subOpt.QoS)),
				slog.Int("RetainHandling", int(subOpt.RetainHandling)),
				slog.Bool("NoLocal", subOpt.NoLocal),
				slog.Bool("RetainAsPublished", subOpt.RetainAsPublished)))
	}

	c.info("send subscribe",
		slog.Group("Subscriptions", (subscriptionGroups)...),
		slog.Any("Properties", sub.Properties))
}

func (c *SessionClient) logUnsubscribe(unsub *paho.Unsubscribe) {
	if unsub == nil {
		return
	}

	var propertiesFields []any
	if unsub.Properties != nil {
		propertiesFields = append(propertiesFields,
			slog.Any("User", unsub.Properties.User))
	}

	topics := make([]any, 0, len(unsub.Topics))
	for _, topic := range unsub.Topics {
		topics = append(topics,
			slog.Group("Topic",
				slog.String("Topic", topic)))
	}

	c.info("send unsubscribe",
		slog.Group("Topics", (topics)...),
		slog.Group("Properties", propertiesFields...))
}

func (c *SessionClient) logPublish(pub *paho.Publish) {
	if pub == nil {
		return
	}

	// Prepare the properties fields
	var propertiesFields []any
	if pub.Properties != nil {
		propertiesFields = append(propertiesFields,
			slog.Any("CorrelationData", pub.Properties.CorrelationData),
			slog.String("ContentType", pub.Properties.ContentType),
			slog.String("ResponseTopic", pub.Properties.ResponseTopic),
			slog.Any("PayloadFormat", pub.Properties.PayloadFormat),
			slog.Any("MessageExpiry", pub.Properties.MessageExpiry),
			slog.Any(
				"SubscriptionIdentifier",
				pub.Properties.SubscriptionIdentifier,
			),
			slog.Any("TopicAlias", pub.Properties.TopicAlias),
			slog.Any("User", pub.Properties.User))
	}

	c.info("send publish",
		slog.Any("PacketID", pub.PacketID),
		slog.Int("QoS", int(pub.QoS)),
		slog.Bool("Retain", pub.Retain),
		slog.String("Topic", pub.Topic),
		slog.Any("Properties", propertiesFields),
		slog.String("Payload", string(pub.Payload)))
}

// logAck logs the published/received message that would be acked.
func (c *SessionClient) logAck(pub *paho.Publish) {
	if pub == nil {
		return
	}

	// Prepare the properties fields
	var propertiesFields []any
	if pub.Properties != nil {
		propertiesFields = append(propertiesFields,
			slog.Any("CorrelationData", pub.Properties.CorrelationData),
			slog.String("ContentType", pub.Properties.ContentType),
			slog.String("ResponseTopic", pub.Properties.ResponseTopic),
			slog.Any("PayloadFormat", pub.Properties.PayloadFormat),
			slog.Any("MessageExpiry", pub.Properties.MessageExpiry),
			slog.Any(
				"SubscriptionIdentifier",
				pub.Properties.SubscriptionIdentifier,
			),
			slog.Any("TopicAlias", pub.Properties.TopicAlias),
			slog.Any("User", pub.Properties.User))
	}

	c.info("send ack",
		slog.Any("PacketID", pub.PacketID),
		slog.Int("QoS", int(pub.QoS)),
		slog.Bool("Retain", pub.Retain),
		slog.String("Topic", pub.Topic),
		slog.Any("Properties", propertiesFields),
		slog.String("Payload", string(pub.Payload)))
}
