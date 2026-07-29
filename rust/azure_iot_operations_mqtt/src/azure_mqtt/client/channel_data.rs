// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types that define requests for an MQTT operation

use crate::azure_mqtt::buffer_pool::Shared;
use crate::azure_mqtt::client::token::{
    acknowledgement::buffered::{PubAckToken, PubRecToken},
    completion::buffered::{
        PubAckCompletionNotifier, PubCompCompletionNotifier, PubRecAcceptCompletionNotifier,
        PubRecRejectCompletionNotifier, PubRelCompletionNotifier, PublishQoS0CompletionNotifier,
        PublishQoS1CompletionNotifier, PublishQoS2CompletionNotifier, ReauthCompletionNotifier,
        SubscribeCompletionNotifier, UnsubscribeCompletionNotifier,
    },
};

use crate::azure_mqtt::mqtt_proto::{
    Auth, ByteStr, Disconnect, Filter, PubAck, PubComp, PubRec, PubRel, Publish,
    PublishOtherProperties, SubscribeOptions, SubscribeOtherProperties, Topic,
    UnsubscribeOtherProperties,
};

// TODO: I don't love the "Request" naming, because it implies a "Response" structure which doens't exist.
// It also isn't symmetrical with the IncomingPublish type.
// Revisit naming.

pub struct DisconnectRequest<S>(pub Disconnect<S>)
where
    S: Shared;

pub struct PublishRequestQoS0<S>(
    pub PublishQoS0CompletionNotifier,
    pub Topic<ByteStr<S>>,
    pub S,
    pub bool,
    pub PublishOtherProperties<S>,
)
where
    S: Shared;

pub enum PublishRequestQoS1QoS2<S>
where
    S: Shared,
{
    PublishQoS1(
        PublishQoS1CompletionNotifier<S>,
        Topic<ByteStr<S>>,
        S,
        bool,
        PublishOtherProperties<S>,
    ),
    PublishQoS2(
        PublishQoS2CompletionNotifier<S>,
        Topic<ByteStr<S>>,
        S,
        bool,
        PublishOtherProperties<S>,
    ),
}

/// Request to send a subscription-related packet
pub enum SubscriptionRequest<S>
where
    S: Shared,
{
    // NOTE: A PUBLISH *is* a control packet, but it is not included here as it has a dedicated
    // channel and enum to allow for prioritization.
    Subscribe(
        SubscribeCompletionNotifier<S>,
        Filter<ByteStr<S>>,
        SubscribeOptions,
        SubscribeOtherProperties<S>,
    ),
    Unsubscribe(
        UnsubscribeCompletionNotifier<S>,
        Filter<ByteStr<S>>,
        UnsubscribeOtherProperties<S>,
    ),
}

/// Request to send an acknowledgement packet
#[allow(clippy::enum_variant_names)]
pub enum AcknowledgementRequest<S>
where
    S: Shared,
{
    // NOTE: Use the user facing packet here because why bother with the composite parts when we
    // have an appropriate structure?
    PubAck(PubAckCompletionNotifier, PubAck<S>, u64),
    PubRecAccept(PubRecAcceptCompletionNotifier<S>, PubRec<S>),
    PubRecReject(PubRecRejectCompletionNotifier, PubRec<S>),
    PubRel(PubRelCompletionNotifier<S>, PubRel<S>),
    PubComp(PubCompCompletionNotifier, PubComp<S>),
}

/// Request to send an AUTH packet (reauth flow)
pub struct ReauthRequest<S>(pub(crate) ReauthCompletionNotifier<S>, pub Auth<S>)
where
    S: Shared;

/// Incoming Publish + Acknowledgement infrastructure
pub enum IncomingPublishAndToken<S>
where
    S: Shared,
{
    QoS0(Publish<S>),
    QoS1(Publish<S>, PubAckToken<S>),
    QoS2(Publish<S>, PubRecToken<S>),
}
