// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Portable triggering of acknowledgement flows

use bytes::Bytes;

use crate::azure_mqtt::client::token::completion::{
    PubAckCompletionToken, PubCompConfirmCompletionToken, PubRecAcceptCompletionToken,
    PubRecRejectCompletionToken, PubRelCompletionToken,
};
use crate::azure_mqtt::error::DetachedError;
use crate::azure_mqtt::packet::{
    PubAckProperties, PubCompProperties, PubRecProperties, PubRejectReason, PubRelProperties,
};

#[derive(Debug)]
pub struct PubAckToken(pub(crate) buffered::PubAckToken<Bytes>);

impl PubAckToken {
    /// Accept the received PUBLISH by issuing a PUBACK indicating success.
    ///
    /// Consumes itself on call, so it cannot be used again.
    ///
    /// Returns once the PUBACK has been accepted into the MQTT session.
    /// The returned `CompletionToken` resolves once the PUBACK is sent (*after* any ordering necessary).
    ///
    /// Can only be successfully used during the same connection epoch on which it was received.
    pub fn accept(
        self,
        properties: PubAckProperties,
    ) -> impl Future<Output = Result<PubAckCompletionToken, DetachedError>> {
        self.0.accept(properties.into())
    }

    /// Reject the received PUBLISH by issuing a PUBACK with an error reason code.
    ///
    /// Consumes itself on call so it cannot be used again.
    ///
    /// Returns once the PUBACK has been accepted into the MQTT session.
    /// The returned `CompletionToken` resolves once the PUBACK is sent (*after* any ordering necessary).
    pub fn reject(
        self,
        reason: PubRejectReason,
        properties: PubAckProperties,
    ) -> impl Future<Output = Result<PubAckCompletionToken, DetachedError>> {
        self.0.reject(reason.into(), properties.into())
    }
}

#[derive(Debug)]
pub struct PubRecToken(pub(crate) buffered::PubRecToken<Bytes>);

impl PubRecToken {
    /// Accept the received PUBLISH by issuing a PUBREC indicating success.
    ///
    /// Consumes itself on call, so it cannot be used again.
    ///
    /// Returns once the PUBREC has been accepted into the MQTT session.
    /// The returned `CompletionToken` resolves once the PUBREC is sent (*after* any ordering necessary).
    ///
    /// Can only be successfully used during the same session epoch on which it was received.
    pub async fn accept(
        self,
        properties: PubRecProperties,
    ) -> Result<PubRecAcceptCompletionToken, DetachedError> {
        self.0
            .accept(properties.into())
            .await
            .map(|token| PubRecAcceptCompletionToken(token.0))
    }

    /// Reject the received PUBLISH by issuing a PUBREC with an error reason code.
    ///
    /// Consumes itself on call so it cannot be used again.
    ///
    /// Returns once the PUBREC has been accepted into the MQTT session.
    /// The returned `CompletionToken` resolves once the PUBREC is sent (*after* any ordering necessary).
    ///
    /// Can only be successfully used during the same session epoch on which it was received.
    pub fn reject(
        self,
        reason: PubRejectReason,
        properties: PubRecProperties,
    ) -> impl Future<Output = Result<PubRecRejectCompletionToken, DetachedError>> {
        self.0.reject(reason.into(), properties.into())
    }
}

/// Token that allows the user to acknowledge a received PUBREC with a PUBREL (QoS 2).
#[derive(Debug)]
pub struct PubRelToken(pub(crate) buffered::PubRelToken<Bytes>);

impl PubRelToken {
    /// Confirm the PUBREC was received by issuing a PUBREL.
    ///
    /// Consumes itself on call so it cannot be used again.
    ///
    /// Returns once the PUBREL has been accepted into the MQTT session.
    /// The returned `CompletionToken` resolves once the PUBREL is sent (*after* any ordering necessary).
    ///
    /// Can only be successfully used during the same session epoch on which it was received.
    pub async fn confirm(
        self,
        properties: PubRelProperties,
    ) -> Result<PubRelCompletionToken, DetachedError> {
        self.0
            .confirm(properties.into())
            .await
            .map(|token| PubRelCompletionToken(token.0))
    }
}

/// Token that allows the user to acknowledge a received PUBREL with a PUBCOMP (QoS 2).
#[derive(Debug)]
pub struct PubCompToken(pub(crate) buffered::PubCompToken<Bytes>);

impl PubCompToken {
    /// Confirm the PUBREL was received by issuing a PUBCOMP.
    ///
    /// Consumes itself on call so it cannot be used again.
    ///
    /// Returns once the PUBCOMP has been accepted into the MQTT session.
    /// The returned `CompletionToken` resolves once the PUBCOMP is sent (*after* any ordering necessary).
    ///
    /// Can only be successfully used during the same session epoch on which it was received.
    pub fn confirm(
        self,
        properties: PubCompProperties,
    ) -> impl Future<Output = Result<PubCompConfirmCompletionToken, DetachedError>> {
        self.0.confirm(properties.into())
    }
}

pub(crate) mod buffered {

    use futures_executor::block_on;
    use tokio::sync::mpsc::Sender;

    use crate::azure_mqtt::buffer_pool::Shared;
    use crate::azure_mqtt::client::channel_data::AcknowledgementRequest;
    use crate::azure_mqtt::client::token::completion::buffered::{
        PubAckCompletionToken, PubCompConfirmCompletionToken, PubRecAcceptCompletionToken,
        PubRecRejectCompletionToken, PubRelConfirmCompletionToken, completion_pair,
    };
    use crate::azure_mqtt::error::DetachedError;
    use crate::azure_mqtt::mqtt_proto::{
        PacketIdentifier, PubAck, PubAckOtherProperties, PubAckReasonCode, PubCompOtherProperties,
        PubRecOtherProperties, PubRecReasonCode, PubRelOtherProperties,
    };

    /// Token that allows the user to acknowledge a received PUBLISH on QoS 1 with a PUBACK.
    #[derive(Debug)]
    pub struct PubAckToken<S>
    where
        S: Shared,
    {
        pkid: PacketIdentifier,
        epoch: u64,
        tx: Sender<AcknowledgementRequest<S>>,
        triggered: bool,
    }

    impl<S> PubAckToken<S>
    where
        S: Shared,
    {
        pub(crate) fn new(
            pkid: PacketIdentifier,
            epoch: u64,
            tx: Sender<AcknowledgementRequest<S>>,
        ) -> Self {
            Self {
                pkid,
                epoch,
                tx,
                triggered: false,
            }
        }

        // NOTE: Even though the return values are the same for these two methods (unlike in PubRecToken),
        // we keep the methods separate for
        // 1) consistency with PubRecToken
        // 2) preventing the illegal 0x10 reason code

        /// Accept the received PUBLISH by issuing a PUBACK indicating success.
        ///
        /// Consumes itself on call, so it cannot be used again.
        ///
        /// Returns once the PUBACK has been accepted into the MQTT session.
        /// The returned `CompletionToken` resolves once the PUBACK is sent (*after* any ordering necessary).
        ///
        /// Can only be successfully used during the same connection epoch on which it was received.
        pub async fn accept(
            self,
            properties: PubAckOtherProperties<S>,
        ) -> Result<PubAckCompletionToken, DetachedError> {
            self.send(properties, PubAckReasonCode::Success).await
        }

        /// Reject the received PUBLISH by issuing a PUBACK with an error reason code.
        ///
        /// Consumes itself on call so it cannot be used again.
        ///
        /// Returns once the PUBACK has been accepted into the MQTT session.
        /// The returned `CompletionToken` resolves once the PUBACK is sent (*after* any ordering necessary).
        pub async fn reject(
            self,
            reason: PubAckReasonCode,
            properties: PubAckOtherProperties<S>,
        ) -> Result<PubAckCompletionToken, DetachedError> {
            self.send(properties, reason).await
        }

        /// Internal helper to send the acknowledgement request.
        async fn send(
            mut self,
            properties: PubAckOtherProperties<S>,
            reason: PubAckReasonCode,
        ) -> Result<PubAckCompletionToken, DetachedError> {
            self.triggered = true;
            PubAckToken::inner_send(&self.tx, self.pkid, properties, reason, self.epoch).await
        }

        /// Internal helper to send the acknowledgement request.
        /// Does not operate on self in order to allow for use in drop efficiently.
        async fn inner_send(
            tx: &Sender<AcknowledgementRequest<S>>,
            packet_identifier: PacketIdentifier,
            other_properties: PubAckOtherProperties<S>,
            reason_code: PubAckReasonCode,
            epoch: u64,
        ) -> Result<PubAckCompletionToken, DetachedError> {
            let (notifier, token) = completion_pair();
            let puback = PubAck {
                packet_identifier,
                reason_code,
                other_properties,
            };
            tx.send(AcknowledgementRequest::PubAck(notifier, puback, epoch))
                .await
                .map_err(|_| DetachedError {})?;
            Ok(PubAckCompletionToken(token))
        }
    }

    impl<S> Drop for PubAckToken<S>
    where
        S: Shared,
    {
        fn drop(&mut self) {
            // Must acknowledge if the token was not used in order to prevent locking the
            // ack ordering flow.
            if !self.triggered {
                // TODO: Consider using Option to avoid cloning for better performance
                let tx = self.tx.clone();
                let pkid = self.pkid;
                let epoch = self.epoch;
                std::thread::spawn(move || {
                    block_on(async move {
                        let _ = PubAckToken::inner_send(
                            &tx,
                            pkid,
                            Default::default(),
                            PubAckReasonCode::Success,
                            epoch,
                        )
                        .await;
                    });
                });
            }
        }
    }

    /// Token that allows the user to acknowledge a received PUBLISH on QoS 2 with a PUBREC.
    #[derive(Debug)]
    pub struct PubRecToken<S>
    where
        S: Shared,
    {
        pkid: PacketIdentifier,
        tx: Sender<AcknowledgementRequest<S>>,
        triggered: bool,
    }

    impl<S> PubRecToken<S>
    where
        S: Shared,
    {
        pub(crate) fn new(pkid: PacketIdentifier, tx: Sender<AcknowledgementRequest<S>>) -> Self {
            Self {
                pkid,
                tx,
                triggered: false,
            }
        }

        /// Accept the received PUBLISH by issuing a PUBREC indicating success.
        ///
        /// Consumes itself on call, so it cannot be used again.
        ///
        /// Returns once the PUBREC has been accepted into the MQTT session.
        /// The returned `CompletionToken` resolves once the PUBREC is sent (*after* any ordering necessary).
        ///
        /// Can only be successfully used during the same session epoch on which it was received.
        pub async fn accept(
            self,
            properties: PubRecOtherProperties<S>,
        ) -> Result<PubRecAcceptCompletionToken<S>, DetachedError> {
            unimplemented!()
        }

        /// Reject the received PUBLISH by issuing a PUBREC with an error reason code.
        ///
        /// Consumes itself on call so it cannot be used again.
        ///
        /// Returns once the PUBREC has been accepted into the MQTT session.
        /// The returned `CompletionToken` resolves once the PUBREC is sent (*after* any ordering necessary).
        ///
        /// Can only be successfully used during the same session epoch on which it was received.
        pub async fn reject(
            self,
            reason: PubRecReasonCode,
            properties: PubRecOtherProperties<S>,
        ) -> Result<PubRecRejectCompletionToken, DetachedError> {
            unimplemented!()
        }
    }

    impl<S> Drop for PubRecToken<S>
    where
        S: Shared,
    {
        fn drop(&mut self) {
            // Must accept
            unimplemented!()
        }
    }

    /// Token that allows the user to acknowledge a received PUBREC with a PUBREL (QoS 2).
    #[derive(Debug)]
    pub struct PubRelToken<S>
    where
        S: Shared,
    {
        pkid: PacketIdentifier,
        tx: Sender<AcknowledgementRequest<S>>,
        triggered: bool,
    }

    impl<S> PubRelToken<S>
    where
        S: Shared,
    {
        pub(crate) fn new(pkid: PacketIdentifier, tx: Sender<AcknowledgementRequest<S>>) -> Self {
            Self {
                pkid,
                tx,
                triggered: false,
            }
        }

        /// Confirm the PUBREC was received by issuing a PUBREL.
        ///
        /// Consumes itself on call so it cannot be used again.
        ///
        /// Returns once the PUBREL has been accepted into the MQTT session.
        /// The returned `CompletionToken` resolves once the PUBREL is sent (*after* any ordering necessary).
        ///
        /// Can only be successfully used during the same session epoch on which it was received.
        pub async fn confirm(
            self,
            properties: PubRelOtherProperties<S>,
        ) -> Result<PubRelConfirmCompletionToken<S>, DetachedError> {
            unimplemented!()
        }
    }

    impl<S> Drop for PubRelToken<S>
    where
        S: Shared,
    {
        fn drop(&mut self) {
            // Must confirm
            unimplemented!()
        }
    }

    /// Token that allows the user to acknowledge a received PUBREL with a PUBCOMP (QoS 2).
    #[derive(Debug)]
    pub struct PubCompToken<S>
    where
        S: Shared,
    {
        pkid: PacketIdentifier,
        tx: Sender<AcknowledgementRequest<S>>,
        triggered: bool,
    }

    impl<S> PubCompToken<S>
    where
        S: Shared,
    {
        pub(crate) fn new(pkid: PacketIdentifier, tx: Sender<AcknowledgementRequest<S>>) -> Self {
            Self {
                pkid,
                tx,
                triggered: false,
            }
        }

        /// Confirm the PUBREL was received by issuing a PUBCOMP.
        ///
        /// Consumes itself on call so it cannot be used again.
        ///
        /// Returns once the PUBCOMP has been accepted into the MQTT session.
        /// The returned `CompletionToken` resolves once the PUBCOMP is sent (*after* any ordering necessary).
        ///
        /// Can only be successfully used during the same session epoch on which it was received.
        pub async fn confirm(
            self,
            properties: PubCompOtherProperties<S>,
        ) -> Result<PubCompConfirmCompletionToken, DetachedError> {
            unimplemented!()
        }
    }

    impl<S> Drop for PubCompToken<S>
    where
        S: Shared,
    {
        fn drop(&mut self) {
            // Must confirm
            unimplemented!()
        }
    }
}

#[cfg(test)]
mod test {
    use bytes::Bytes;

    use super::buffered::*;
    use crate::azure_mqtt::client::channel_data::AcknowledgementRequest;
    use crate::azure_mqtt::mqtt_proto::{PacketIdentifier, PubAckOtherProperties, PubAckReasonCode};

    #[tokio::test]
    async fn puback_token_accept() {
        let (tx, mut rx) = tokio::sync::mpsc::channel(1);
        let pkid = PacketIdentifier::new(1).unwrap();
        let epoch = 3;
        let properties = PubAckOtherProperties {
            reason_string: Some("Test Success".into()),
            user_properties: vec![
                ("key1".into(), "value1".into()),
                ("key2".into(), "value2".into()),
            ],
        };
        let token = PubAckToken::new(pkid, epoch, tx);
        let completion_token = token.accept(properties.clone()).await.unwrap();
        if let Some(AcknowledgementRequest::PubAck(notifier, puback, req_epoch)) = rx.recv().await {
            // The correct data was sent in the acknowledgement request
            assert_eq!(req_epoch, epoch);
            assert_eq!(puback.packet_identifier, pkid);
            assert_eq!(puback.reason_code, PubAckReasonCode::Success);
            assert_eq!(puback.other_properties, properties);
            // Using the acknowledgement request notifier completes the completion token that was returned
            let completion_value = ();
            notifier.complete(completion_value).unwrap();
            assert_eq!(completion_token.await, Ok(completion_value));
        } else {
            panic!("Did not receive PubAck acknowledgement request");
        }
    }

    #[tokio::test]
    async fn puback_token_reject() {
        let (tx, mut rx) = tokio::sync::mpsc::channel(1);
        let pkid = PacketIdentifier::new(1).unwrap();
        let epoch = 3;
        let properties = PubAckOtherProperties {
            reason_string: Some("Test Reject".into()),
            user_properties: vec![
                ("key1".into(), "value1".into()),
                ("key2".into(), "value2".into()),
            ],
        };
        let token = PubAckToken::new(pkid, epoch, tx);
        let completion_token = token
            .reject(PubAckReasonCode::NotAuthorized, properties.clone())
            .await
            .unwrap();
        if let Some(AcknowledgementRequest::PubAck(notifier, puback, req_epoch)) = rx.recv().await {
            assert_eq!(req_epoch, epoch);
            assert_eq!(puback.packet_identifier, pkid);
            assert_eq!(puback.reason_code, PubAckReasonCode::NotAuthorized);
            assert_eq!(puback.other_properties, properties);
            // Using the acknowledgement request notifier completes the completion token that was returned
            let completion_value = ();
            notifier.complete(completion_value).unwrap();
            assert_eq!(completion_token.await, Ok(completion_value));
        } else {
            panic!("Did not receive PubAck acknowledgement request");
        }
    }

    #[tokio::test]
    async fn puback_token_drop_before_use() {
        let (tx, mut rx) = tokio::sync::mpsc::channel(1);
        let pkid = PacketIdentifier::new(1).unwrap();
        let epoch = 3;
        let token = PubAckToken::<Bytes>::new(pkid, epoch, tx);
        // Drop the token without accepting or rejecting it
        drop(token);
        // It was accepted automatically with default properties
        if let Some(AcknowledgementRequest::PubAck(_, puback, req_epoch)) = rx.recv().await {
            assert_eq!(req_epoch, epoch);
            assert_eq!(puback.packet_identifier, pkid);
            assert_eq!(puback.reason_code, PubAckReasonCode::Success);
            assert_eq!(puback.other_properties, Default::default());
        } else {
            panic!("Did not receive PubAck acknowledgement request");
        }
        // There are no additional items in the channel (i.e. was only accepted once)
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert_eq!(rx.len(), 0);
    }

    #[tokio::test]
    async fn puback_token_drop_after_use() {
        let (tx, mut rx) = tokio::sync::mpsc::channel(1);
        let pkid = PacketIdentifier::new(1).unwrap();
        let epoch = 3;
        let properties = PubAckOtherProperties {
            reason_string: Some("Test Success".into()),
            user_properties: vec![
                ("key1".into(), "value1".into()),
                ("key2".into(), "value2".into()),
            ],
        };
        let token = PubAckToken::new(pkid, epoch, tx);
        // Use the token to send an acceptance
        let completion_token = token.accept(properties.clone()).await.unwrap();
        if let Some(AcknowledgementRequest::PubAck(_, puback, req_epoch)) = rx.recv().await {
            assert_eq!(req_epoch, epoch);
            assert_eq!(puback.packet_identifier, pkid);
            assert_eq!(puback.reason_code, PubAckReasonCode::Success);
            assert_eq!(puback.other_properties, properties);
        } else {
            panic!("Did not receive PubAck acknowledgement request");
        }
        // There are currently no other items in the channel
        assert_eq!(rx.len(), 0);
        // Now drop the token
        drop(completion_token);
        // There should still be no additional items in the channel (i.e. was only accepted once)
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        assert_eq!(rx.len(), 0);
    }
}
