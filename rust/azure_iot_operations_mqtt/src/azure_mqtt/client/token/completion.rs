// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

//! Synchronization for portable reporting of remote operations

use bytes::Bytes;
use thiserror::Error;

/// Indicates a failure that occurred during the completion of an MQTT operation.
#[derive(Clone, PartialEq, Debug, Error)]
pub enum CompletionError {
    #[error("Communication channels with the client have been closed")]
    Detached,
    #[error("The operation was canceled due to {0}")]
    Canceled(String),
}

// TODO: can we make this only available in the crate?
macro_rules! make_completion_token_ty {
    ($vis:vis struct $token_ty:ident $( < $($ty_param_name:ident : $ty_param_bound:path ),* > )? (CompletionToken< $element_ty:ty >)) => {
        #[derive(Debug)]
        $vis struct $token_ty $(< $($ty_param_name : $ty_param_bound),* >)? (pub(crate) crate::azure_mqtt::client::token::completion::buffered::CompletionToken<$element_ty>);

        impl $(< $($ty_param_name : $ty_param_bound),* >)? std::future::Future for $token_ty $(< $($ty_param_name ),* >)? {
            type Output = Result<$element_ty, $crate::azure_mqtt::client::token::completion::CompletionError>;

            fn poll(
                mut self: std::pin::Pin<&mut Self>,
                cx: &mut std::task::Context<'_>,
            ) -> std::task::Poll<Self::Output> {
                std::pin::Pin::new(&mut self.0).poll(cx)
            }
        }
    };

    ($vis:vis struct $token_ty:ident (CompletionToken< $original_element_ty:ty > -> $element_ty:ty $map_fn:block )) => {
        #[derive(Debug)]
        $vis struct $token_ty(pub(crate) buffered::CompletionToken<$original_element_ty>);

        impl std::future::Future for $token_ty {
            type Output = Result<$element_ty, $crate::azure_mqtt::client::token::completion::CompletionError>;

            fn poll(
                mut self: std::pin::Pin<&mut Self>,
                cx: &mut std::task::Context<'_>,
            ) -> std::task::Poll<Self::Output> {
                match std::pin::Pin::new(&mut self.0).poll(cx) {
                    std::task::Poll::Ready(Ok(value)) => {
                        std::task::Poll::Ready(Ok(($map_fn)(value)))
                    }
                    std::task::Poll::Ready(Err(_)) => {
                        std::task::Poll::Ready(Err($crate::azure_mqtt::client::token::completion::CompletionError::Detached))
                    }
                    std::task::Poll::Pending => std::task::Poll::Pending,
                }
            }
        }
    };
}

make_completion_token_ty!(pub struct PublishQoS0CompletionToken(CompletionToken<()>));

make_completion_token_ty!(pub struct PublishQoS1CompletionToken(CompletionToken<crate::azure_mqtt::mqtt_proto::PubAck<Bytes>> -> crate::azure_mqtt::packet::PubAck { Into::into }));

make_completion_token_ty!(pub struct PublishQoS2CompletionToken(
    CompletionToken<(
        crate::azure_mqtt::mqtt_proto::PubRec<Bytes>,
        Option<crate::azure_mqtt::client::token::acknowledgement::buffered::PubRelToken<Bytes>>,
    )> -> (
        crate::azure_mqtt::packet::PubRec,
        Option<crate::azure_mqtt::client::token::acknowledgement::PubRelToken>,
    ) {
        |(pubrec, token): (_, Option<_>)| (crate::azure_mqtt::packet::PubRec::from(pubrec), token.map(crate::azure_mqtt::client::token::acknowledgement::PubRelToken))
    }
));

make_completion_token_ty!(pub struct PubRecAcceptCompletionToken(
    CompletionToken<(
        crate::azure_mqtt::mqtt_proto::PubRel<Bytes>,
        crate::azure_mqtt::client::token::acknowledgement::buffered::PubCompToken<Bytes>,
    )> -> (
        crate::azure_mqtt::packet::PubRel,
        crate::azure_mqtt::client::token::acknowledgement::PubCompToken,
    ) {
        |(pubrel, pubcomp_token)| (crate::azure_mqtt::packet::PubRel::from(pubrel), crate::azure_mqtt::client::token::acknowledgement::PubCompToken(pubcomp_token))
    }
));

make_completion_token_ty!(pub struct PubRecRejectCompletionToken(CompletionToken<()>));

make_completion_token_ty!(pub struct PubRelCompletionToken(CompletionToken<crate::azure_mqtt::mqtt_proto::PubComp<Bytes>> -> crate::azure_mqtt::packet::PubComp { Into::into }));

make_completion_token_ty!(pub struct SubscribeCompletionToken(CompletionToken<crate::azure_mqtt::mqtt_proto::SubAck<Bytes>> -> crate::azure_mqtt::packet::SubAck { Into::into }));

make_completion_token_ty!(pub struct UnsubscribeCompletionToken(CompletionToken<crate::azure_mqtt::mqtt_proto::UnsubAck<Bytes>> -> crate::azure_mqtt::packet::UnsubAck { Into::into }));

make_completion_token_ty!(pub struct ReauthCompletionToken(CompletionToken<crate::azure_mqtt::client::buffered::ReauthResult<Bytes>> -> crate::azure_mqtt::client::ReauthResult { Into::into }));

make_completion_token_ty!(pub struct PubAckCompletionToken(CompletionToken<()>));

make_completion_token_ty!(pub struct PubCompConfirmCompletionToken(CompletionToken<()>));

pub(crate) mod buffered {
    use std::future::Future;
    use std::pin::Pin;
    use std::task::Poll;

    use tokio::sync::oneshot;

    use super::CompletionError;
    use crate::azure_mqtt::buffer_pool::Shared;
    use crate::azure_mqtt::client::buffered::ReauthResult;
    use crate::azure_mqtt::client::token::acknowledgement::buffered::{PubCompToken, PubRelToken};
    use crate::azure_mqtt::mqtt_proto::{PubAck, PubComp, PubRec, PubRel, SubAck, UnsubAck}; // TODO

    /// Create a new completion pair, consisting of a [`CompletionNotifier`] and a [`CompletionToken`].
    pub fn completion_pair<T>() -> (CompletionNotifier<T>, CompletionToken<T>) {
        let (tx, rx) = oneshot::channel();
        let token = CompletionToken(rx);
        let notifier = CompletionNotifier(tx);
        (notifier, token)
    }

    // NOTE: Currently there are not buffered equivalents for all tokens defined in the main module.
    // This is because they are not currently used, but that may at some point be desirable.

    pub use super::{
        PubAckCompletionToken, PubCompConfirmCompletionToken, PubRecRejectCompletionToken,
    };

    make_completion_token_ty!(pub struct ReauthCompletionToken<S: Shared>(CompletionToken<ReauthResult<S>>));
    make_completion_token_ty!(pub struct PubRecAcceptCompletionToken<S: Shared>(CompletionToken<(PubRel<S>, PubCompToken<S>)>));
    make_completion_token_ty!(pub struct PubRelConfirmCompletionToken<S: Shared>(CompletionToken<PubComp<S>>));

    // Aliases for completion notifier types.
    // For internal use where we'd prefer to avoid the mix of user-facing and internal packet types.
    pub(crate) type PublishQoS0CompletionNotifier = CompletionNotifier<()>;
    pub(crate) type PublishQoS1CompletionNotifier<S> = CompletionNotifier<PubAck<S>>;
    pub(crate) type PublishQoS2CompletionNotifier<S> =
        CompletionNotifier<(PubRec<S>, Option<PubRelToken<S>>)>;
    pub(crate) type SubscribeCompletionNotifier<S> = CompletionNotifier<SubAck<S>>;
    pub(crate) type UnsubscribeCompletionNotifier<S> = CompletionNotifier<UnsubAck<S>>;
    pub(crate) type PubAckCompletionNotifier = CompletionNotifier<()>;
    pub(crate) type PubRecAcceptCompletionNotifier<S> =
        CompletionNotifier<(PubRel<S>, PubCompToken<S>)>;
    pub(crate) type PubRecRejectCompletionNotifier = CompletionNotifier<()>;
    pub(crate) type PubRelCompletionNotifier<S> = CompletionNotifier<PubComp<S>>;
    pub(crate) type PubCompCompletionNotifier = CompletionNotifier<()>;
    pub(crate) type ReauthCompletionNotifier<S> = CompletionNotifier<ReauthResult<S>>;

    #[derive(Debug)]
    pub struct CompletionToken<T>(oneshot::Receiver<Result<T, CompletionError>>);

    impl<T> Future for CompletionToken<T> {
        type Output = Result<T, CompletionError>;

        fn poll(
            mut self: std::pin::Pin<&mut Self>,
            cx: &mut std::task::Context<'_>,
        ) -> Poll<Self::Output> {
            match Pin::new(&mut self.0).poll(cx) {
                Poll::Ready(Ok(value)) => Poll::Ready(value),
                Poll::Ready(Err(_)) => Poll::Ready(Err(CompletionError::Detached)),
                Poll::Pending => Poll::Pending,
            }
        }
    }

    /// Notifier half of a completion pair
    #[derive(Debug)]
    pub(crate) struct CompletionNotifier<T>(oneshot::Sender<Result<T, CompletionError>>);

    impl<T> CompletionNotifier<T> {
        /// Complete the associated token(s) with the given value.
        /// If all the token(s) have been dropped, the value is returned.
        pub fn complete(self, value: T) -> Result<(), T> {
            match self.0.send(Ok(value)) {
                Ok(()) => Ok(()),
                Err(Ok(v)) => Err(v),
                Err(Err(_)) => unreachable!(),
            }
        }

        /// Issue a cancellation to the associated token(s).
        /// If all the token(s) have been dropped, an error is returned.
        pub fn cancel(self, reason: &str) -> Result<(), String> {
            match self
                .0
                .send(Err(CompletionError::Canceled(reason.to_string())))
            {
                Ok(()) => Ok(()),
                Err(Ok(_)) => unreachable!(),
                Err(Err(_)) => Err("Token dropped".to_string()),
            }
        }

        // TODO:
        // What other failures could there be other than cancellation?
        // Do they need distinct methods?
        // - packet size?
        // - wildcard sub?
        // - qos exceeded?
        // - connect while connected i.e. state error
    }
}

#[cfg(test)]
mod test {
    use super::CompletionError;
    use super::buffered::*;

    #[tokio::test]
    async fn simple_completion() {
        let (notifier, token) = completion_pair();

        notifier.complete("hello_world".to_string()).unwrap();

        let res = token.await;
        assert_eq!(res, Ok("hello_world".to_string()));
    }

    #[tokio::test]
    async fn simple_cancellation() {
        let (notifier, token): (CompletionNotifier<String>, CompletionToken<String>) =
            completion_pair();

        notifier.cancel("test").unwrap();

        let res = token.await;
        assert_eq!(res, Err(CompletionError::Canceled("test".to_string())));
    }

    #[tokio::test]
    async fn portability_completion() {
        let (notifier, token) = completion_pair();

        let handle = tokio::spawn(token);

        notifier.complete("hello_world".to_string()).unwrap();

        let res = handle.await.unwrap();
        assert_eq!(res, Ok("hello_world".to_string()));
    }

    #[tokio::test]
    async fn portability_cancellation() {
        let (notifier, token): (CompletionNotifier<String>, CompletionToken<String>) =
            completion_pair();

        let handle = tokio::spawn(token);

        notifier.cancel("test").unwrap();

        let res = handle.await.unwrap();
        assert_eq!(res, Err(CompletionError::Canceled("test".to_string())));
    }

    #[tokio::test]
    async fn dropped_token() {
        let (notifier, token): (CompletionNotifier<String>, CompletionToken<String>) =
            completion_pair();

        drop(token);

        let res = notifier.complete("hello_world".to_string());
        assert_eq!(res, Err("hello_world".to_string()));
    }

    #[tokio::test]
    async fn dropped_notifier() {
        let (notifier, token): (CompletionNotifier<String>, CompletionToken<String>) =
            completion_pair();

        drop(notifier);

        let res = token.await;
        assert_eq!(res, Err(CompletionError::Detached));
    }
}
