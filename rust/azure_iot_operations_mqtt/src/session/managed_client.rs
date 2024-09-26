
use std::collections::HashSet;
use std::sync::{Arc, Mutex};

use async_trait::async_trait;
use bytes::Bytes;
use tokio::sync::mpsc::Receiver;

use crate::CompletionToken;
use crate::control_packet::{Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties};
use crate::error::ClientError;
use crate::interface::{MqttAck, MqttPubReceiver, MqttPubSub};
use crate::session::pub_tracker::PubTracker;


// impl<C, EL> MqttProvider<SessionPubSub<C>, SessionPubReceiver> for Session<C, EL>
// where
//     C: InternalClient + Clone + Send + Sync + 'static,
//     EL: MqttEventLoop,
// {
//     /// Return the client ID of the MQTT client being used in this [`Session`]
//     fn client_id(&self) -> &str {
//         &self.client_id
//     }

//     /// Return an instance of [`SessionPubSub`] that can be used to execute MQTT operations
//     /// using this [`Session`]
//     fn pub_sub(&self) -> SessionPubSub<C> {
//         SessionPubSub(self.client.clone())
//     }

//     /// Return an instance of [`SessionPubReceiver`] that can be used to receive incoming publishes
//     /// on a particular topic using this [`Session`]
//     ///
//     /// # Arguments
//     /// * `topic_filter` - The topic filter to use for the receiver
//     /// * `auto_ack` - Whether the receiver should automatically ack incoming publishes
//     ///
//     /// # Errors
//     /// Returns a [`TopicParseError`] if the pub receiver cannot be registered.
//     fn filtered_pub_receiver(
//         &mut self,
//         topic_filter: &str,
//         auto_ack: bool,
//     ) -> Result<SessionPubReceiver, TopicParseError> {
//         let topic_filter = TopicFilter::from_str(topic_filter)?;
//         let rx = self.incoming_pub_dispatcher.register_filter(&topic_filter);
//         Ok(SessionPubReceiver::new(
//             rx,
//             self.unacked_pubs.clone(),
//             auto_ack,
//         ))
//     }
// }


/// Send outgoing MQTT messages for publish, subscribe and unsubscribe.
// TODO: MORE DOC
#[derive(Clone)]
struct SessionManagedClient<PS>
where
    PS: MqttPubSub + Clone + Send + Sync,
{
    client_id: String,
    pub_sub: PS
}

#[async_trait]
impl<PS> MqttPubSub for SessionManagedClient<PS>
where
    PS: MqttPubSub + Clone + Send + Sync,
{
    async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, ClientError> {
        self.pub_sub.publish(topic, qos, retain, payload).await
    }

    async fn publish_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
        properties: PublishProperties,
    ) -> Result<CompletionToken, ClientError> {
        self.pub_sub
            .publish_with_properties(topic, qos, retain, payload, properties)
            .await
    }

    async fn subscribe(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
    ) -> Result<CompletionToken, ClientError> {
        match qos {
            QoS::AtMostOnce => {
                unimplemented!("QoS 0 is not yet supported for subscribe operations")
            }
            QoS::AtLeastOnce => self.pub_sub.subscribe(topic, qos).await,
            QoS::ExactlyOnce => {
                unimplemented!("QoS 2 is not yet supported for subscribe operations")
            }
        }
    }

    async fn subscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        properties: SubscribeProperties,
    ) -> Result<CompletionToken, ClientError> {
        match qos {
            QoS::AtMostOnce => {
                unimplemented!("QoS 0 is not yet supported for subscribe operations")
            }
            QoS::AtLeastOnce => {
                self.pub_sub
                    .subscribe_with_properties(topic, qos, properties)
                    .await
            }
            QoS::ExactlyOnce => {
                unimplemented!("QoS 2 is not yet supported for subscribe operations")
            }
        }
    }

    async fn unsubscribe(
        &self,
        topic: impl Into<String> + Send,
    ) -> Result<CompletionToken, ClientError> {
        self.pub_sub.unsubscribe(topic).await
    }

    async fn unsubscribe_with_properties(
        &self,
        topic: impl Into<String> + Send,
        properties: UnsubscribeProperties,
    ) -> Result<CompletionToken, ClientError> {
        self.pub_sub.unsubscribe_with_properties(topic, properties).await
    }
}




/// Receive and acknowledge incoming MQTT messages.
pub struct SessionPubReceiver {
    /// Receiver for incoming publishes
    pub_rx: Receiver<Publish>,
    /// Tracker for acks of incoming publishes
    unacked_pubs: Arc<PubTracker>,
    /// Controls whether incoming publishes are auto-acked
    auto_ack: bool,
    /// Set of PKIDs for incoming publishes that have not yet been acked.
    /// Ensures publishes cannot be acked twice.
    /// (only used if `auto_ack` == false)
    unacked_pkids: Mutex<HashSet<u16>>,
}

impl SessionPubReceiver {
    pub fn new(pub_rx: Receiver<Publish>, unacked_pubs: Arc<PubTracker>, auto_ack: bool) -> Self {
        Self {
            pub_rx,
            unacked_pubs,
            auto_ack,
            unacked_pkids: Mutex::new(HashSet::new()),
        }
    }
}

#[async_trait]
impl MqttPubReceiver for SessionPubReceiver {
    async fn recv(&mut self) -> Option<Publish> {
        let result = self.pub_rx.recv().await;
        if let Some(publish) = &result {
            if self.auto_ack {
                // Ack immediately if auto-ack is enabled
                // TODO: This ack failure should probably be unreachable and cause panic.
                // Reconsider in error PR.
                self.unacked_pubs.ack(publish).await.unwrap();
            } else {
                // Otherwise, track the PKID for manual acking
                self.unacked_pkids.lock().unwrap().insert(publish.pkid);
            }
        }

        result
    }
}

#[async_trait]
impl MqttAck for SessionPubReceiver {
    async fn ack(&self, publish: &Publish) -> Result<(), ClientError> {
        {
            let mut unacked_pkids_g = self.unacked_pkids.lock().unwrap();
            // TODO: don't panic here. This is bad.
            // Will be addressed in next PR about errors, but don't want to expand
            // the scope of this one.
            assert!(!self.auto_ack, "Auto-ack is enabled. Cannot manually ack.");
            assert!(unacked_pkids_g.contains(&publish.pkid), "");
            unacked_pkids_g.remove(&publish.pkid);
        }
        // TODO: Convert this error into the correct type
        self.unacked_pubs.ack(publish).await.unwrap();
        Ok(())
    }
}

impl Drop for SessionPubReceiver {
    fn drop(&mut self) {
        // Close the receiver channel to ensure no more publishes are dispatched
        // while we clean up.
        self.pub_rx.close();

        // Drain and ack any remaining publishes that are in flight so as not to
        // hold up the ack ordering.
        //
        // NOTE: We MUST do this because if not, the pub tracker can enter a bad state.
        // Consider a SessionPubReceiver that drops while the Session remains alive,
        // where there are dispatched messages in the pub_rx channel. This puts the
        // PubTracker (and thus the Session) in a bad state. There will be an item in
        // it awaiting acks that will never come, thus blocking all other acks from being
        // able to be sent due to ordering rules. Once a publish is dispatched to a
        // SessionPubReceiver, the SessionPubReceiver MUST ack them all.
        while let Ok(publish) = self.pub_rx.try_recv() {
            // NOTE: Not ideal to spawn these tasks in a drop, but it can be safely
            // done here by moving the necessary values.
            log::warn!(
                "Dropping SessionPubReceiver with unacked publish (PKID {}). Auto-acking.",
                publish.pkid
            );
            tokio::task::spawn({
                let unacked_pubs = self.unacked_pubs.clone();
                let publish = publish;
                async move {
                    match unacked_pubs.ack(&publish).await {
                        Ok(()) => log::debug!("Auto-ack of PKID {} successful", publish.pkid),
                        Err(e) => log::error!(
                            "Auto-ack failed for {}. Publish may be redelivered. Reason: {e:?}",
                            publish.pkid
                        ),
                        // TODO: if this ack failed, the Session is now in a broken state. Consider adding an
                        // emergency mechanism of some kind to get us out of it.
                    };
                }
            });
        }
    }
}