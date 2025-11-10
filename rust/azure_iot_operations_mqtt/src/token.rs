// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Structures representing the various tokens used in MQTT client operations

pub type PublishQoS0CompletionToken =
    azure_mqtt::client::token::completion::PublishQoS0CompletionToken;
pub type PublishQoS1CompletionToken =
    azure_mqtt::client::token::completion::PublishQoS1CompletionToken;
pub use crate::session::dispatcher::AckCompletionToken;
pub type SubscribeCompletionToken = azure_mqtt::client::token::completion::SubscribeCompletionToken;
pub type UnsubscribeCompletionToken =
    azure_mqtt::client::token::completion::UnsubscribeCompletionToken;
