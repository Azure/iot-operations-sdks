// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, Owned, Shared};
use crate::azure_mqtt::mqtt_proto::{
    BinaryData, ByteStr, PacketIdentifier, PacketIdentifierDupQoS, Publish, PublishOtherProperties,
    QoS, Topic, UserProperties,
};

/// A message that can be published to the server
/// but might not yet assigned a packet identifier.
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct Publication<S>
where
    S: Shared,
{
    pub topic_name: Topic<ByteStr<S>>,
    pub qos: QoS,
    pub retain: bool,
    pub payload: S,
    pub other_properties: PublicationOtherProperties<S>,
}

impl<S> Publication<S>
where
    S: Shared,
{
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<Publication<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let response_topic = match &self.other_properties.response_topic {
            Some(value) => Some(value.to_shared(owned)?),
            None => None,
        };

        let correlation_data = match &self.other_properties.correlation_data {
            Some(value) => Some(value.to_shared(owned)?),
            None => None,
        };

        let mut user_properties = Vec::with_capacity(self.other_properties.user_properties.len());
        for (key, val) in &self.other_properties.user_properties {
            let key = key.to_shared(owned)?;
            let val = val.to_shared(owned)?;
            user_properties.push((key, val));
        }

        let content_type = match &self.other_properties.content_type {
            Some(value) => Some(value.to_shared(owned)?),
            None => None,
        };

        let payload = self.payload.copy_to_shared(owned)?;

        Ok(Publication {
            topic_name: self.topic_name.to_shared(owned)?,
            qos: self.qos,
            retain: self.retain,
            other_properties: PublicationOtherProperties {
                payload_is_utf8: self.other_properties.payload_is_utf8,
                message_expiry_interval: self.other_properties.message_expiry_interval,
                response_topic,
                correlation_data,
                user_properties,
                content_type,
            },
            payload,
        })
    }
}

impl<S> From<Publish<S>> for Publication<S>
where
    S: Shared,
{
    fn from(publish: Publish<S>) -> Self {
        Self {
            topic_name: publish.topic_name,
            qos: publish.packet_identifier_dup_qos.qos(),
            retain: publish.retain,
            payload: publish.payload,
            other_properties: publish.other_properties.into(),
        }
    }
}

impl<S> From<Publication<S>> for Publish<S>
where
    S: Shared,
{
    fn from(publication: Publication<S>) -> Self {
        let packet_id = PacketIdentifier::new(1).expect("valid default packet id");
        let packet_identifier_dup_qos = match publication.qos {
            QoS::AtMostOnce => PacketIdentifierDupQoS::AtMostOnce,
            QoS::AtLeastOnce => PacketIdentifierDupQoS::AtLeastOnce(packet_id, false),
            QoS::ExactlyOnce => PacketIdentifierDupQoS::ExactlyOnce(packet_id, false),
        };

        Publish {
            topic_name: publication.topic_name,
            packet_identifier_dup_qos,
            retain: publication.retain,
            payload: publication.payload,
            other_properties: publication.other_properties.into(),
        }
    }
}

impl<S> From<(Publication<S>, PacketIdentifierDupQoS)> for Publish<S>
where
    S: Shared,
{
    fn from(
        (publication, packet_identifier_dup_qos): (Publication<S>, PacketIdentifierDupQoS),
    ) -> Self {
        Publish {
            topic_name: publication.topic_name,
            packet_identifier_dup_qos,
            retain: publication.retain,
            payload: publication.payload,
            other_properties: publication.other_properties.into(),
        }
    }
}

#[derive(Clone, Debug)]
#[derive_where(Default, Eq, PartialEq)]
pub struct PublicationOtherProperties<S>
where
    S: Shared,
{
    pub payload_is_utf8: bool,
    pub message_expiry_interval: Option<u32>,
    pub response_topic: Option<Topic<ByteStr<S>>>,
    pub correlation_data: Option<BinaryData<S>>,
    pub user_properties: UserProperties<S>,
    pub content_type: Option<ByteStr<S>>,
}

impl<S> From<PublishOtherProperties<S>> for PublicationOtherProperties<S>
where
    S: Shared,
{
    fn from(props: PublishOtherProperties<S>) -> Self {
        Self {
            payload_is_utf8: props.payload_is_utf8,
            message_expiry_interval: props.message_expiry_interval,
            response_topic: props.response_topic,
            correlation_data: props.correlation_data,
            user_properties: props.user_properties,
            content_type: props.content_type,
        }
    }
}
