// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::env;
use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::{
    ApplicationContext, ApplicationContextOptionsBuilder,
};
use iso8601_duration;

const AVRO_SERVER_ID: &str = "AvroRustServer";
const JSON_SERVER_ID: &str = "JsonRustServer";
const RAW_SERVER_ID: &str = "RawRustServer";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;

#[derive(PartialEq)]
enum CommFormat {
    Avro,
    Json,
    Raw,
}

#[tokio::main(flavor = "current_thread")]
async fn main() {
    let args: Vec<String> = env::args().collect();

    if args.len() < 3 {
        println!(
            "Usage: {} {{AVRO|JSON|RAW}} iterations [interval_in_seconds]",
            args[0]
        );
        return;
    }

    let (format, server_id) = match args[1].to_lowercase().as_str() {
        "avro" => (CommFormat::Avro, AVRO_SERVER_ID),
        "json" => (CommFormat::Json, JSON_SERVER_ID),
        "raw" => (CommFormat::Raw, RAW_SERVER_ID),
        _ => {
            println!("format must be AVRO or JSON or RAW");
            return;
        }
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

    let mqtt_client = session.create_managed_client();
    let exit_handle = session.create_exit_handle();
    match format {
        CommFormat::Avro => tokio::task::spawn(avro_telemetry_loop(
            mqtt_client,
            exit_handle,
            iterations,
            interval,
        )),
        CommFormat::Json => tokio::task::spawn(json_telemetry_loop(
            mqtt_client,
            exit_handle,
            iterations,
            interval,
        )),
        CommFormat::Raw => tokio::task::spawn(raw_telemetry_loop(
            mqtt_client,
            exit_handle,
            iterations,
            interval,
        )),
    };

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
    let application_context =
        ApplicationContext::new(ApplicationContextOptionsBuilder::default().build().unwrap());

    let sender_options =
        avro_comm::common_types::common_options::TelemetryOptionsBuilder::default()
            .build()
            .unwrap();

    let telemetry_sender: avro_comm::avro_model::service::TelemetrySender<_> =
        avro_comm::avro_model::service::TelemetrySender::new(
            application_context,
            client,
            &sender_options,
        );

    println!("Starting send loop");
    println!();

    for i in 0..iterations {
        println!("  Sending iteration {i}");

        let mut builder = avro_comm::avro_model::service::TelemetryMessageBuilder::default();
        let telemetry = avro_comm::avro_model::service::TelemetryCollection {
            lengths: Some(vec![i.into(), (i + 1).into(), (i + 2).into()]),
            proximity: Some(if i % 3 == 0 {
                avro_comm::avro_model::service::ProximitySchema::Far
            } else {
                avro_comm::avro_model::service::ProximitySchema::Near
            }),
            schedule: Some(avro_comm::avro_model::service::ScheduleSchema {
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
    let application_context =
        ApplicationContext::new(ApplicationContextOptionsBuilder::default().build().unwrap());

    let sender_options =
        json_comm::common_types::common_options::TelemetryOptionsBuilder::default()
            .build()
            .unwrap();

    let telemetry_sender: json_comm::json_model::service::TelemetrySender<_> =
        json_comm::json_model::service::TelemetrySender::new(
            application_context,
            client,
            &sender_options,
        );

    println!("Starting send loop");
    println!();

    for i in 0..iterations {
        println!("  Sending iteration {i}");

        let mut builder = json_comm::json_model::service::TelemetryMessageBuilder::default();
        let telemetry = json_comm::json_model::service::TelemetryCollection {
            lengths: Some(vec![i.into(), (i + 1).into(), (i + 2).into()]),
            proximity: Some(if i % 3 == 0 {
                json_comm::json_model::service::ProximitySchema::Far
            } else {
                json_comm::json_model::service::ProximitySchema::Near
            }),
            schedule: Some(json_comm::json_model::service::ScheduleSchema {
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

async fn raw_telemetry_loop(
    client: SessionManagedClient,
    exit_handle: SessionExitHandle,
    iterations: i32,
    interval: Duration,
) {
    let application_context =
        ApplicationContext::new(ApplicationContextOptionsBuilder::default().build().unwrap());

    let sender_options = raw_comm::common_types::common_options::TelemetryOptionsBuilder::default()
        .build()
        .unwrap();

    let telemetry_sender: raw_comm::raw_model::service::TelemetrySender<_> =
        raw_comm::raw_model::service::TelemetrySender::new(
            application_context,
            client,
            &sender_options,
        );

    println!("Starting send loop");
    println!();

    for i in 0..iterations {
        println!("  Sending iteration {i}");

        let mut builder = raw_comm::raw_model::service::TelemetryMessageBuilder::default();
        let telemetry = format!("Sample data {i}");

        let message = builder
            .payload(telemetry.into_bytes())
            .unwrap()
            .build()
            .unwrap();
        telemetry_sender.send(message).await.unwrap();

        tokio::time::sleep(interval).await;
    }

    exit_handle.try_exit().await.unwrap();
}
