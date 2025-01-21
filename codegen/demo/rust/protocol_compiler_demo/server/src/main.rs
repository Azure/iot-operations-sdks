// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::env;
use std::time::Duration;

use avro_comm;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use iso8601_duration;
use json_comm;

const AVRO_SERVER_ID: &str = "AvroRustServer";
const JSON_SERVER_ID: &str = "JsonRustServer";
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
        println!(
            "Usage: {} {{AVRO|JSON}} iterations [interval_in_seconds]",
            args[0]
        );
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

    let server_id = if format == CommFormat::Avro {
        AVRO_SERVER_ID
    } else {
        JSON_SERVER_ID
    };

    let iterations = args[2].parse::<i32>().unwrap();

    let interval = Duration::from_secs(if args.len() > 3 {
        args[3].parse::<u64>().unwrap()
    } else {
        1
    });

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(server_id)
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

    print!("Connecting to MQTT broker as {server_id} ... ");
    let mut session = Session::new(session_options).unwrap();
    println!("Connected!");

    if format == CommFormat::Avro {
        tokio::task::spawn(avro_telemetry_loop(
            session.create_managed_client(),
            session.create_exit_handle(),
            iterations,
            interval,
        ));
    } else {
        tokio::task::spawn(json_telemetry_loop(
            session.create_managed_client(),
            session.create_exit_handle(),
            iterations,
            interval,
        ));
    }

    session.run().await.unwrap();

    println!();
    println!("Stopping send loop");
}

async fn avro_telemetry_loop(
    client: SessionManagedClient,
    exit_handle: SessionExitHandle,
    iterations: i32,
    interval: Duration,
) {
    let sender_options =
        avro_comm::common_types::common_options::TelemetryOptionsBuilder::default()
            .build()
            .unwrap();

    let telemetry_sender: avro_comm::dtmi_codegen_communicationTest_avroModel__1::service::TelemetryCollectionSender<_> =
        avro_comm::dtmi_codegen_communicationTest_avroModel__1::service::TelemetryCollectionSender::new(client, &sender_options);

    println!("Starting send loop");
    println!();

    for i in 0..iterations {
        println!("  Sending iteration {i}");

        let mut builder = avro_comm::dtmi_codegen_communicationTest_avroModel__1::service::TelemetryCollectionMessageBuilder::default();
        let telemetry = avro_comm::dtmi_codegen_communicationTest_avroModel__1::service::TelemetryCollection {
            lengths: Some(vec![i.into(), (i + 1).into(), (i + 2).into()]),
            proximity: Some(if i % 3 == 0 {
                    avro_comm::dtmi_codegen_communicationTest_avroModel__1::service::Enum_Proximity::Far
                } else {
                    avro_comm::dtmi_codegen_communicationTest_avroModel__1::service::Enum_Proximity::Near
                }),
            schedule: Some(avro_comm::dtmi_codegen_communicationTest_avroModel__1::service::Object_Schedule {
                course: Some("Math".to_string()),
                credit: Some(format!("{:0>2}:{:0>2}:{:0>2}", i + 2, i + 1, i)),
            }),
        };

        let message = builder.payload(telemetry).unwrap().build().unwrap();
        telemetry_sender.send(message).await.unwrap();

        tokio::time::sleep(interval).await;
    }

    exit_handle.try_exit().await.unwrap();
}

async fn json_telemetry_loop(
    client: SessionManagedClient,
    exit_handle: SessionExitHandle,
    iterations: i32,
    interval: Duration,
) {
    let sender_options =
        json_comm::common_types::common_options::TelemetryOptionsBuilder::default()
            .build()
            .unwrap();

    let telemetry_sender: json_comm::dtmi_codegen_communicationTest_jsonModel__1::service::TelemetryCollectionSender<_> =
        json_comm::dtmi_codegen_communicationTest_jsonModel__1::service::TelemetryCollectionSender::new(client, &sender_options);

    println!("Starting send loop");
    println!();

    for i in 0..iterations {
        println!("  Sending iteration {i}");

        let mut builder = json_comm::dtmi_codegen_communicationTest_jsonModel__1::service::TelemetryCollectionMessageBuilder::default();
        let telemetry = json_comm::dtmi_codegen_communicationTest_jsonModel__1::service::TelemetryCollection {
            lengths: Some(vec![i.into(), (i + 1).into(), (i + 2).into()]),
            proximity: Some(if i % 3 == 0 {
                    json_comm::dtmi_codegen_communicationTest_jsonModel__1::service::Enum_Proximity::Far
                } else {
                    json_comm::dtmi_codegen_communicationTest_jsonModel__1::service::Enum_Proximity::Near
                }),
            schedule: Some(json_comm::dtmi_codegen_communicationTest_jsonModel__1::service::Object_Schedule {
                course: Some("Math".to_string()),
                credit: Some(iso8601_duration::Duration {
                    year: 0.0,
                    month: 0.0,
                    day: 0.0,
                    hour: i as f32 + 2.0,
                    minute: i as f32 + 1.0,
                    second: i as f32,
                }),
            }),
        };

        let message = builder.payload(telemetry).unwrap().build().unwrap();
        telemetry_sender.send(message).await.unwrap();

        tokio::time::sleep(interval).await;
    }

    exit_handle.try_exit().await.unwrap();
}
