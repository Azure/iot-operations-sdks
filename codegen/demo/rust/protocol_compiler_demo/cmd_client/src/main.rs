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
use counters::counter_collection::client::{
    ConditionSchema, GetLocationCommandInvoker, GetLocationRequestBuilder, GetLocationRequestPayloadBuilder,
    IncrementCommandInvoker, IncrementRequestBuilder, IncrementRequestPayloadBuilder,
};

const CLIENT_ID: &str = "RustCounterClient";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;

#[derive(PartialEq)]
enum CounterCommand {
    Increment,
    GetLocation,
}

#[tokio::main(flavor = "current_thread")]
async fn main() {
    let args: Vec<String> = env::args().collect();

    if args.len() < 3 {
        println!("Usage: {} {{INC|GET}} counter_name", args[0]);
        return;
    }

    let command = match args[1].to_lowercase().as_str() {
        "inc" => CounterCommand::Increment,
        "get" => CounterCommand::GetLocation,
        _ => {
            println!("command must be INC or GET");
            return;
        }
    };

    let counter_name = args[2].clone();

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
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

    print!("Connecting to MQTT broker as {CLIENT_ID} ... ");
    let session = Session::new(session_options).unwrap();
    println!("Connected!");

    let mqtt_client = session.create_managed_client();
    let exit_handle = session.create_exit_handle();

    match command {
        CounterCommand::Increment => tokio::task::spawn(send_increment_command(mqtt_client, exit_handle, counter_name)),
        CounterCommand::GetLocation => tokio::task::spawn(send_get_location_command(mqtt_client, exit_handle, counter_name)),
    };

    session.run().await.unwrap();
}

async fn send_increment_command(
    mqtt_client: SessionManagedClient,
    exit_handle: SessionExitHandle,
    counter_name: String,
) {
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let options = counters::common_types::common_options::CommandOptionsBuilder::default()
        .build()
        .unwrap();
    let increment_invoker =
        IncrementCommandInvoker::new(application_context.clone(), mqtt_client.clone(), &options);

    let increment_payload = IncrementRequestPayloadBuilder::default()
        .counter_name(counter_name.clone())
        .build()
        .unwrap();

    let increment_request = IncrementRequestBuilder::default()
        .payload(increment_payload)
        .unwrap()
        .timeout(Duration::from_secs(10))
        .build()
        .unwrap();

    let response = increment_invoker.invoke(increment_request).await;

    match response {
        Ok(Ok(response)) => {
            println!(
                "New value = {}",
                response.payload.counter_value
            );
        }
        Ok(Err(counter_error)) => {
            println!("Request failed with error: '{}'", counter_error);
            match counter_error.condition {
                Some(ConditionSchema::CounterNotFound) => {
                    println!("Counter '{counter_name}' was not found");
                },
                Some(ConditionSchema::CounterOverflow) => {
                    println!("Counter '{counter_name}' has overflowed");
                },
                None => {},
            };
        }
        Err(err) => {
            println!("Protocol error = {err:?}");
        }
    }

    exit_handle.try_exit().await.unwrap();
}

async fn send_get_location_command(
    mqtt_client: SessionManagedClient,
    exit_handle: SessionExitHandle,
    counter_name: String,
) {
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let options = counters::common_types::common_options::CommandOptionsBuilder::default()
        .build()
        .unwrap();
    let get_location_invoker =
        GetLocationCommandInvoker::new(application_context.clone(), mqtt_client.clone(), &options);

    let get_location_payload = GetLocationRequestPayloadBuilder::default()
        .counter_name(counter_name.clone())
        .build()
        .unwrap();

    let get_location_request = GetLocationRequestBuilder::default()
        .payload(get_location_payload)
        .unwrap()
        .timeout(Duration::from_secs(10))
        .build()
        .unwrap();

    let response = get_location_invoker.invoke(get_location_request).await;

    match response {
        Ok(Ok(response)) => {
            match response.payload.counter_location {
                Some(location) => {
                    println!(
                        "Location = ({}, {})",
                        location.latitude,
                        location.longitude,
                    );
                }
                None => {
                    println!("counter is not deployed in the field");
                }
            }
        }
        Ok(Err(counter_error)) => {
            println!("Request failed with error: '{}'", counter_error);
            match counter_error.condition {
                Some(ConditionSchema::CounterNotFound) => {
                    println!("Counter '{counter_name}' was not found");
                },
                Some(ConditionSchema::CounterOverflow) => {
                    println!("Counter '{counter_name}' has overflowed");
                },
                None => {},
            };
        }
        Err(err) => {
            println!("Protocol error = {err:?}");
        }
    }

    exit_handle.try_exit().await.unwrap();
}
