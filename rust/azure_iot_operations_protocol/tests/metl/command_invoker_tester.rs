// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::sync::atomic;

use crate::metl::defaults::InvokerDefaults;
use crate::metl::test_case::TestCase;
use crate::metl::test_case_action::TestCaseAction;
use crate::metl::test_case_catch::TestCaseCatch;
use crate::metl::test_case_invoker::TestCaseInvoker;

static TEST_CASE_INDEX: atomic::AtomicI32 = atomic::AtomicI32::new(0);

struct PseudoInvoker {}

pub async fn test_command_invoker(test_case: &TestCase<InvokerDefaults>, use_session_client: bool) {
    assert!(!use_session_client);

    let test_case_index = TEST_CASE_INDEX.fetch_add(1, atomic::Ordering::Relaxed);

    let _mqtt_client_id: String =
        if let Some(client_id) = test_case.prologue.mqtt_config.client_id.as_ref() {
            client_id.clone()
        } else {
            let client_id_prefix = if use_session_client {
                "Session"
            } else {
                "Standalone"
            };
            format!("{client_id_prefix}InvokerTestClient{test_case_index}")
        };

    // TODO: create stub client

    // TODO: connect stub client

    if let Some(push_acks) = test_case.prologue.push_acks.as_ref() {
        for _ack_kind in &push_acks.publish {
            // TODO: push acks
        }

        for _ack_kind in &push_acks.subscribe {
            // TODO: push acks
        }

        for _ack_kind in &push_acks.unsubscribe {
            // TODO: push acks
        }
    }

    let mut invokers: HashMap<String, PseudoInvoker> = HashMap::new();

    let invoker_count = test_case.prologue.invokers.len();
    let mut ix = 0;
    for test_case_invoker in &test_case.prologue.invokers {
        ix += 1;
        let catch = if ix == invoker_count {
            test_case.prologue.catch.as_ref()
        } else {
            None
        };

        let wrapped_invoker = get_command_invoker(test_case_invoker, catch);
        if wrapped_invoker.is_none() {
            return;
        }

        let invoker = wrapped_invoker.unwrap();

        if let Some(command_name) = test_case_invoker.command_name.as_ref() {
            invokers.insert(command_name.clone(), invoker);
        }
    }

    for test_case_action in &test_case.actions {
        match test_case_action {
            action_invoke_command @ TestCaseAction::InvokeCommand { .. } => {
                invoke_command(action_invoke_command);
            }
            action_await_invocation @ TestCaseAction::AwaitInvocation { .. } => {
                await_invocation(action_await_invocation);
            }
            action_receive_response @ TestCaseAction::ReceiveResponse { .. } => {
                receive_response(action_receive_response);
            }
            action_await_ack @ TestCaseAction::AwaitAck { .. } => {
                await_acknowledgement(action_await_ack);
            }
            action_await_publish @ TestCaseAction::AwaitPublish { .. } => {
                await_publish(action_await_publish);
            }
            action_sleep @ TestCaseAction::Sleep { .. } => {
                sleep(action_sleep);
            }
            action_disconnect @ TestCaseAction::Disconnect { .. } => {
                disconnect(action_disconnect);
            }
            action_freeze_time @ TestCaseAction::FreezeTime { .. } => {
                freeze_time(action_freeze_time);
            }
            action_unfreeze_time @ TestCaseAction::UnfreezeTime { .. } => {
                unfreeze_time(action_unfreeze_time);
            }
            _ => {
                panic!("unexpected action kind");
            }
        }
    }

    if let Some(test_case_epilogue) = test_case.epilogue.as_ref() {
        for _topic in &test_case_epilogue.subscribed_topics {
            // TODO check that topic has been subscribed
        }

        if let Some(_publication_count) = test_case_epilogue.publication_count {
            // TODO check publication count
        }

        for _published_message in &test_case_epilogue.published_messages {
            // TODO check that message has been published
        }

        if let Some(_acknowledgement_count) = test_case_epilogue.acknowledgement_count {
            // TODO check acknowledgement count
        }
    }
}

fn get_command_invoker(
    _tci: &TestCaseInvoker<InvokerDefaults>,
    _catch: Option<&TestCaseCatch>,
) -> Option<PseudoInvoker> {
    return Some(PseudoInvoker {});
}

fn invoke_command(action: &TestCaseAction<InvokerDefaults>) {
    if let TestCaseAction::InvokeCommand {
        defaults_type: _,
        invocation_index: _,
        command_name: _,
        executor_id: _,
        timeout: _,
        request_value: _,
        metadata: _,
    } = action
    {
    } else {
        panic!("internal logic error");
    }
}

fn await_invocation(action: &TestCaseAction<InvokerDefaults>) {
    if let TestCaseAction::AwaitInvocation {
        defaults_type: _,
        invocation_index: _,
        response_value: _,
        metadata: _,
        catch: _,
    } = action
    {
    } else {
        panic!("internal logic error");
    }
}

fn receive_response(action: &TestCaseAction<InvokerDefaults>) {
    if let TestCaseAction::ReceiveResponse {
        defaults_type: _,
        topic: _,
        payload: _,
        bypass_serialization: _,
        content_type: _,
        format_indicator: _,
        metadata: _,
        correlation_index: _,
        qos: _,
        message_expiry: _,
        status: _,
        status_message: _,
        is_application_error: _,
        invalid_property_name: _,
        invalid_property_value: _,
        packet_index: _,
    } = action
    {
    } else {
        panic!("internal logic error");
    }
}

fn await_acknowledgement(action: &TestCaseAction<InvokerDefaults>) {
    if let TestCaseAction::AwaitAck {
        defaults_type: _,
        packet_index: _,
    } = action
    {
    } else {
        panic!("internal logic error");
    }
}

fn await_publish(action: &TestCaseAction<InvokerDefaults>) {
    if let TestCaseAction::AwaitPublish {
        defaults_type: _,
        correlation_index: _,
    } = action
    {
    } else {
        panic!("internal logic error");
    }
}

fn sleep(action: &TestCaseAction<InvokerDefaults>) {
    if let TestCaseAction::Sleep {
        defaults_type: _,
        duration: _,
    } = action
    {
    } else {
        panic!("internal logic error");
    }
}

fn disconnect(action: &TestCaseAction<InvokerDefaults>) {
    if let TestCaseAction::Disconnect { defaults_type: _ } = action {
    } else {
        panic!("internal logic error");
    }
}

fn freeze_time(action: &TestCaseAction<InvokerDefaults>) {
    if let TestCaseAction::FreezeTime { defaults_type: _ } = action {
    } else {
        panic!("internal logic error");
    }
}

fn unfreeze_time(action: &TestCaseAction<InvokerDefaults>) {
    if let TestCaseAction::UnfreezeTime { defaults_type: _ } = action {
    } else {
        panic!("internal logic error");
    }
}
