// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::marker::PhantomData;

use serde::Deserialize;

use crate::metl::defaults::DefaultsType;
use crate::metl::optional_field::deserialize_optional_field;
use crate::metl::test_case_action_invoke_command::{self};
use crate::metl::test_case_action_receive_request::{self};
use crate::metl::test_case_action_receive_response::{self};
use crate::metl::test_case_action_receive_telemetry::{self};
use crate::metl::test_case_action_send_telemetry::{self};
use crate::metl::test_case_catch::TestCaseCatch;
use crate::metl::test_case_cloud_event::TestCaseCloudEvent;
use crate::metl::test_case_duration::TestCaseDuration;

#[derive(Clone, Deserialize, Debug)]
#[serde(tag = "action")]
#[allow(dead_code)]
pub enum TestCaseAction<T: DefaultsType + Default> {
    #[serde(rename = "await acknowledgement")]
    AwaitAck {
        #[serde(default)]
        defaults_type: PhantomData<T>,

        #[serde(rename = "packet-index")]
        packet_index: Option<i32>,
    },

    #[serde(rename = "await invocation")]
    AwaitInvocation {
        #[serde(default)]
        defaults_type: PhantomData<T>,

        #[serde(rename = "invocation-index")]
        invocation_index: i32,

        #[serde(rename = "response-value")]
        #[serde(default)]
        #[serde(deserialize_with = "deserialize_optional_field")]
        #[allow(clippy::option_option)]
        response_value: Option<Option<String>>,

        #[serde(rename = "metadata")]
        metadata: Option<HashMap<String, String>>,

        #[serde(rename = "catch")]
        catch: Option<TestCaseCatch>,
    },

    #[serde(rename = "await publish")]
    AwaitPublish {
        #[serde(default)]
        defaults_type: PhantomData<T>,

        #[serde(rename = "correlation-index")]
        correlation_index: Option<i32>,
    },

    #[serde(rename = "await send")]
    AwaitSend {
        #[serde(default)]
        defaults_type: PhantomData<T>,

        #[serde(rename = "catch")]
        catch: Option<TestCaseCatch>,
    },

    #[serde(rename = "disconnect")]
    Disconnect {
        #[serde(default)]
        defaults_type: PhantomData<T>,
    },

    #[serde(rename = "freeze time")]
    FreezeTime {
        #[serde(default)]
        defaults_type: PhantomData<T>,
    },

    #[serde(rename = "invoke command")]
    InvokeCommand {
        #[serde(default)]
        defaults_type: PhantomData<T>,

        #[serde(rename = "invocation-index")]
        invocation_index: i32,

        #[serde(rename = "command-name")]
        #[serde(default = "test_case_action_invoke_command::get_default_command_name::<T>")]
        command_name: Option<String>,

        #[serde(rename = "topic-token-map")]
        topic_token_map: Option<HashMap<String, String>>,

        #[serde(rename = "timeout")]
        #[serde(default = "test_case_action_invoke_command::get_default_timeout::<T>")]
        timeout: Option<TestCaseDuration>,

        #[serde(rename = "request-value")]
        #[serde(default = "test_case_action_invoke_command::get_default_request_value::<T>")]
        request_value: Option<String>,

        #[serde(rename = "metadata")]
        metadata: Option<HashMap<String, String>>,
    },

    #[serde(rename = "receive request")]
    ReceiveRequest {
        #[serde(default)]
        defaults_type: PhantomData<T>,

        #[serde(rename = "topic")]
        #[serde(default = "test_case_action_receive_request::get_default_topic::<T>")]
        topic: Option<String>,

        #[serde(rename = "payload")]
        #[serde(default = "test_case_action_receive_request::get_default_payload::<T>")]
        payload: Option<String>,

        #[serde(rename = "bypass-serialization")]
        #[serde(default)]
        bypass_serialization: bool,

        #[serde(rename = "content-type")]
        #[serde(default = "test_case_action_receive_request::get_default_content_type::<T>")]
        content_type: Option<String>,

        #[serde(rename = "format-indicator")]
        #[serde(default = "test_case_action_receive_request::get_default_format_indicator::<T>")]
        format_indicator: Option<u8>,

        #[serde(rename = "metadata")]
        #[serde(default)]
        metadata: HashMap<String, String>,

        #[serde(rename = "correlation-index")]
        #[serde(default = "test_case_action_receive_request::get_default_correlation_index::<T>")]
        correlation_index: Option<i32>,

        #[serde(rename = "correlation-id")]
        correlation_id: Option<String>,

        #[serde(rename = "qos")]
        #[serde(default = "test_case_action_receive_request::get_default_qos::<T>")]
        qos: Option<i32>,

        #[serde(rename = "message-expiry")]
        #[serde(default = "test_case_action_receive_request::get_default_message_expiry::<T>")]
        message_expiry: Option<TestCaseDuration>,

        #[serde(rename = "response-topic")]
        #[serde(default = "test_case_action_receive_request::get_default_response_topic::<T>")]
        response_topic: Option<String>,

        #[serde(rename = "source-index")]
        #[serde(default = "test_case_action_receive_request::get_default_source_index::<T>")]
        source_index: Option<i32>,

        #[serde(rename = "packet-index")]
        packet_index: Option<i32>,
    },

    #[serde(rename = "receive response")]
    ReceiveResponse {
        #[serde(default)]
        defaults_type: PhantomData<T>,

        #[serde(rename = "topic")]
        #[serde(default = "test_case_action_receive_response::get_default_topic::<T>")]
        topic: Option<String>,

        #[serde(rename = "payload")]
        #[serde(default = "test_case_action_receive_response::get_default_payload::<T>")]
        payload: Option<String>,

        #[serde(rename = "bypass-serialization")]
        #[serde(default)]
        bypass_serialization: bool,

        #[serde(rename = "content-type")]
        #[serde(default = "test_case_action_receive_response::get_default_content_type::<T>")]
        content_type: Option<String>,

        #[serde(rename = "format-indicator")]
        #[serde(default = "test_case_action_receive_response::get_default_format_indicator::<T>")]
        format_indicator: Option<u8>,

        #[serde(rename = "metadata")]
        #[serde(default)]
        metadata: HashMap<String, String>,

        #[serde(rename = "correlation-index")]
        #[serde(default = "test_case_action_receive_response::get_default_correlation_index::<T>")]
        correlation_index: Option<i32>,

        #[serde(rename = "qos")]
        #[serde(default = "test_case_action_receive_response::get_default_qos::<T>")]
        qos: Option<i32>,

        #[serde(rename = "message-expiry")]
        #[serde(default = "test_case_action_receive_response::get_default_message_expiry::<T>")]
        message_expiry: Option<TestCaseDuration>,

        #[serde(rename = "status")]
        #[serde(default = "test_case_action_receive_response::get_default_status::<T>")]
        status: Option<String>,

        #[serde(rename = "status-message")]
        #[serde(default = "test_case_action_receive_response::get_default_status_message::<T>")]
        status_message: Option<String>,

        #[serde(rename = "is-application-error")]
        #[serde(
            default = "test_case_action_receive_response::get_default_is_application_error::<T>"
        )]
        is_application_error: Option<String>,

        #[serde(rename = "invalid-property-name")]
        #[serde(
            default = "test_case_action_receive_response::get_default_invalid_property_name::<T>"
        )]
        invalid_property_name: Option<String>,

        #[serde(rename = "invalid-property-value")]
        #[serde(
            default = "test_case_action_receive_response::get_default_invalid_property_value::<T>"
        )]
        invalid_property_value: Option<String>,

        #[serde(rename = "packet-index")]
        packet_index: Option<i32>,
    },

    #[serde(rename = "receive telemetry")]
    ReceiveTelemetry {
        #[serde(default)]
        defaults_type: PhantomData<T>,

        #[serde(rename = "topic")]
        #[serde(default = "test_case_action_receive_telemetry::get_default_topic::<T>")]
        topic: Option<String>,

        #[serde(rename = "payload")]
        #[serde(default = "test_case_action_receive_telemetry::get_default_payload::<T>")]
        payload: Option<String>,

        #[serde(rename = "bypass-serialization")]
        #[serde(default)]
        bypass_serialization: bool,

        #[serde(rename = "content-type")]
        #[serde(default = "test_case_action_receive_telemetry::get_default_content_type::<T>")]
        content_type: Option<String>,

        #[serde(rename = "format-indicator")]
        #[serde(default = "test_case_action_receive_telemetry::get_default_format_indicator::<T>")]
        format_indicator: Option<u8>,

        #[serde(rename = "metadata")]
        #[serde(default)]
        metadata: HashMap<String, String>,

        #[serde(rename = "qos")]
        #[serde(default = "test_case_action_receive_telemetry::get_default_qos::<T>")]
        qos: Option<i32>,

        #[serde(rename = "message-expiry")]
        #[serde(default = "test_case_action_receive_telemetry::get_default_message_expiry::<T>")]
        message_expiry: Option<TestCaseDuration>,

        #[serde(rename = "source-index")]
        #[serde(default = "test_case_action_receive_telemetry::get_default_source_index::<T>")]
        source_index: Option<i32>,

        #[serde(rename = "packet-index")]
        packet_index: Option<i32>,
    },

    #[serde(rename = "send telemetry")]
    SendTelemetry {
        #[serde(default)]
        defaults_type: PhantomData<T>,

        #[serde(rename = "telemetry-name")]
        #[serde(default = "test_case_action_send_telemetry::get_default_telemetry_name::<T>")]
        telemetry_name: Option<String>,

        #[serde(rename = "timeout")]
        #[serde(default = "test_case_action_send_telemetry::get_default_timeout::<T>")]
        timeout: Option<TestCaseDuration>,

        #[serde(rename = "telemetry-value")]
        #[serde(default = "test_case_action_send_telemetry::get_default_telemetry_value::<T>")]
        telemetry_value: Option<String>,

        #[serde(rename = "metadata")]
        metadata: Option<HashMap<String, String>>,

        #[serde(rename = "cloud-event")]
        cloud_event: Option<TestCaseCloudEvent>,

        #[serde(rename = "qos")]
        #[serde(default = "test_case_action_send_telemetry::get_default_qos::<T>")]
        qos: Option<i32>,
    },

    #[serde(rename = "sleep")]
    Sleep {
        #[serde(default)]
        defaults_type: PhantomData<T>,

        #[serde(rename = "duration")]
        duration: TestCaseDuration,
    },

    #[serde(rename = "sync")]
    Sync {
        #[serde(default)]
        defaults_type: PhantomData<T>,

        #[serde(rename = "signal-event")]
        signal_event: Option<String>,

        #[serde(rename = "wait-event")]
        wait_event: Option<String>,
    },

    #[serde(rename = "unfreeze time")]
    UnfreezeTime {
        #[serde(default)]
        defaults_type: PhantomData<T>,
    },
}
