// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::env;
use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use json_comm;

const AVRO_CLIENT_ID: &str = "AvroRustClient";
const JSON_CLIENT_ID: &str = "JsonRustClient";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;

#[derive(PartialEq)]
enum CommFormat {
    Avro,
    Json,
}

#[tokio::main(flavor = "current_thread")]
async fn main() {
    let args: Vec<String> = env::args().collect();

    if args.len() < 3 {
        println!("Usage: {} {{AVRO|JSON}} seconds_to_run", args[0]);
        return;
    }

    let format = match args[1].to_lowercase().as_str() {
        "avro" => CommFormat::Avro,
        "json" => CommFormat::Json,
        _ => {
            println!("format must be AVRO or JSON");
            return;
        }
    };

    let client_id = if format == CommFormat::Avro {
        AVRO_CLIENT_ID
    } else {
        JSON_CLIENT_ID
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
    let mut session = Session::new(session_options).unwrap();
    println!("Connected!");

    if format == CommFormat::Avro {
        tokio::task::spawn(avro_telemetry_loop(session.create_managed_client()));
    } else {
        tokio::task::spawn(json_telemetry_loop(session.create_managed_client()));
    }

    tokio::spawn(exit_timer(session.create_exit_handle(), run_duration));

    session.run().await.unwrap();

    println!("Stopping receive loop");
}

async fn avro_telemetry_loop(client: SessionManagedClient) {
    let receiver_options =
        avro_comm::common_types::common_options::TelemetryOptionsBuilder::default()
            .build()
            .unwrap();

    let mut telemetry_receiver: avro_comm::dtmi_codegen_communicationTest_avroModel__1::client::TelemetryCollectionReceiver<_> =
        avro_comm::dtmi_codegen_communicationTest_avroModel__1::client::TelemetryCollectionReceiver::new(client, &receiver_options);

    println!("Starting receive loop");
    println!();

    while let Some(message) = telemetry_receiver.recv().await {
        match message {
            Ok((message, _)) => {
                let sender_id = message.sender_id.unwrap();

                println!("Received telemetry from {sender_id}....");

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
    let receiver_options =
        json_comm::common_types::common_options::TelemetryOptionsBuilder::default()
            .build()
            .unwrap();

    let mut telemetry_receiver: json_comm::dtmi_codegen_communicationTest_jsonModel__1::client::TelemetryCollectionReceiver<_> =
        json_comm::dtmi_codegen_communicationTest_jsonModel__1::client::TelemetryCollectionReceiver::new(client, &receiver_options);

    println!("Starting receive loop");
    println!();

    while let Some(message) = telemetry_receiver.recv().await {
        match message {
            Ok((message, _)) => {
                let sender_id = message.sender_id.unwrap();

                println!("Received telemetry from {sender_id}....");

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
