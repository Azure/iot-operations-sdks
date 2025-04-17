// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::env;
use std::str;
use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::ApplicationContextBuilder;

const AVRO_CLIENT_ID: &str = "AvroRustClient";
const JSON_CLIENT_ID: &str = "JsonRustClient";
const RAW_CLIENT_ID: &str = "RawRustClient";
const CUSTOM_CLIENT_ID: &str = "CustomRustClient";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;

#[derive(PartialEq)]
enum CommFormat {
    Avro,
    Json,
    Raw,
    Custom,
}

#[tokio::main(flavor = "current_thread")]
async fn main() {
    let args: Vec<String> = env::args().collect();

    if args.len() < 3 {
        println!("Usage: {} {{AVRO|JSON|RAW|CUSTOM}} seconds_to_run", args[0]);
        return;
    }

    let (format, client_id) = match args[1].to_lowercase().as_str() {
        "avro" => (CommFormat::Avro, AVRO_CLIENT_ID),
        "json" => (CommFormat::Json, JSON_CLIENT_ID),
        "raw" => (CommFormat::Raw, RAW_CLIENT_ID),
        "custom" => (CommFormat::Custom, CUSTOM_CLIENT_ID),
        _ => {
            println!("format must be AVRO or JSON or RAW or CUSTOM");
            return;
        }
    };

    let run_duration = Duration::from_secs(args[2].parse::<u64>().unwrap());

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()
        .unwrap();

    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();

    print!("Connecting to MQTT broker as {client_id} ... ");
    let session = Session::new(session_options).unwrap();
    println!("Connected!");

    let mqtt_client = session.create_managed_client();
    match format {
        CommFormat::Avro => tokio::task::spawn(avro_telemetry_loop(mqtt_client)),
        CommFormat::Json => tokio::task::spawn(json_telemetry_loop(mqtt_client)),
        CommFormat::Raw => tokio::task::spawn(raw_telemetry_loop(mqtt_client)),
        CommFormat::Custom => tokio::task::spawn(custom_telemetry_loop(mqtt_client)),
    };

    tokio::spawn(exit_timer(session.create_exit_handle(), run_duration));

    session.run().await.unwrap();

    println!("Stopping receive loop");
}

async fn avro_telemetry_loop(client: SessionManagedClient) {
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let receiver_options =
        avro_comm::common_types::options::TelemetryReceiverOptionsBuilder::default()
            .build()
            .unwrap();

    let mut telemetry_receiver: avro_comm::avro_model::client::TelemetryReceiver<_> =
        avro_comm::avro_model::client::TelemetryReceiver::new(
            application_context,
            client,
            &receiver_options,
        );

    println!("Starting receive loop");
    println!();

    while let Some(message) = telemetry_receiver.recv().await {
        match message {
            Ok((message, _)) => {
                let sender_id = message.sender_id.unwrap();
                let my_replacement = message.topic_tokens.get("ex:myToken").unwrap();

                println!("Received telemetry from {sender_id} with token replacement {my_replacement}....");

                if let Some(schedule) = message.payload.schedule {
                    if let Some(course) = schedule.course {
                        if let Some(credit) = schedule.credit {
                            println!("  Schedule: course \"{course}\" => {credit}");
                        }
                    }
                }

                if let Some(lengths) = message.payload.lengths {
                    print!("  Lengths:");
                    for length in lengths {
                        print!(" {length}");
                    }
                    println!();
                }

                if let Some(proximity) = message.payload.proximity {
                    println!("  Proximity: {proximity:?}");
                }

                if let Some(data) = message.payload.data {
                    println!("  Data: {}", str::from_utf8(&data).unwrap());
                }

                println!();
            }
            Err(e) => {
                println!("Error receiving telemetry message: {e:?}");
                break;
            }
        }
    }
}

async fn json_telemetry_loop(client: SessionManagedClient) {
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let receiver_options =
        json_comm::common_types::options::TelemetryReceiverOptionsBuilder::default()
            .build()
            .unwrap();

    let mut telemetry_receiver: json_comm::json_model::client::TelemetryReceiver<_> =
        json_comm::json_model::client::TelemetryReceiver::new(
            application_context,
            client,
            &receiver_options,
        );

    println!("Starting receive loop");
    println!();

    while let Some(message) = telemetry_receiver.recv().await {
        match message {
            Ok((message, _)) => {
                let sender_id = message.sender_id.unwrap();
                let my_replacement = message.topic_tokens.get("ex:myToken").unwrap();

                println!("Received telemetry from {sender_id} with token replacement {my_replacement}....");

                if let Some(schedule) = message.payload.schedule {
                    if let Some(course) = schedule.course {
                        if let Some(credit) = schedule.credit {
                            println!(
                                "  Schedule: course \"{course}\" => {:0>2}:{:0>2}:{:0>2}",
                                credit.hour, credit.minute, credit.second
                            );
                        }
                    }
                }

                if let Some(lengths) = message.payload.lengths {
                    print!("  Lengths:");
                    for length in lengths {
                        print!(" {length}");
                    }
                    println!();
                }

                if let Some(proximity) = message.payload.proximity {
                    println!("  Proximity: {proximity:?}");
                }

                if let Some(data) = message.payload.data {
                    println!("  Data: {}", str::from_utf8(&data).unwrap());
                }

                println!();
            }
            Err(e) => {
                println!("Error receiving telemetry message: {e:?}");
                break;
            }
        }
    }
}

async fn raw_telemetry_loop(client: SessionManagedClient) {
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let receiver_options =
        raw_comm::common_types::options::TelemetryReceiverOptionsBuilder::default()
            .build()
            .unwrap();

    let mut telemetry_receiver: raw_comm::raw_model::client::TelemetryReceiver<_> =
        raw_comm::raw_model::client::TelemetryReceiver::new(
            application_context,
            client,
            &receiver_options,
        );

    println!("Starting receive loop");
    println!();

    while let Some(message) = telemetry_receiver.recv().await {
        match message {
            Ok((message, _)) => {
                let sender_id = message.sender_id.unwrap();
                let my_replacement = message.topic_tokens.get("ex:myToken").unwrap();

                let data = str::from_utf8(&message.payload).unwrap();
                println!("Received telemetry from {sender_id} with token replacement {my_replacement}....");
                println!("  Data: {data:?}");

                println!();
            }
            Err(e) => {
                println!("Error receiving telemetry message: {e:?}");
                break;
            }
        }
    }
}

async fn custom_telemetry_loop(client: SessionManagedClient) {
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let receiver_options =
        custom_comm::common_types::options::TelemetryReceiverOptionsBuilder::default()
            .build()
            .unwrap();

    let mut telemetry_receiver: custom_comm::custom_model::client::TelemetryReceiver<_> =
        custom_comm::custom_model::client::TelemetryReceiver::new(
            application_context,
            client,
            &receiver_options,
        );

    println!("Starting receive loop");
    println!();

    while let Some(message) = telemetry_receiver.recv().await {
        match message {
            Ok((message, _)) => {
                let sender_id = message.sender_id.unwrap();
                let my_replacement = message.topic_tokens.get("ex:myToken").unwrap();
                let content_type = &message.payload.content_type.as_str();

                let payload = str::from_utf8(&message.payload.payload).unwrap();
                println!(
                    "Received telemetry from {sender_id} with content type {content_type} and token replacement {my_replacement}...."
                );
                println!("  Payload: {payload:?}");

                println!();
            }
            Err(e) => {
                println!("Error receiving telemetry message: {e:?}");
                break;
            }
        }
    }
}

async fn exit_timer(exit_handle: SessionExitHandle, exit_after: Duration) {
    tokio::time::sleep(exit_after).await;
    exit_handle.try_exit().await.unwrap();
}
