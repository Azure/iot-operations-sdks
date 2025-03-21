// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::env;
use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use counters::common_types::common_options::CommandOptionsBuilder;
use counters::counter_collection::service::{
    ConditionSchema, CounterError, CounterLocation,
    GetLocationCommandExecutor, GetLocationResponseBuilder, GetLocationResponsePayload,
    IncrementCommandExecutor, IncrementResponseBuilder, IncrementResponsePayload,
};

const SERVER_ID: &str = "RustCounterServer";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;

#[tokio::main(flavor = "current_thread")]
async fn main() {
    let args: Vec<String> = env::args().collect();

    if args.len() < 2 {
        println!("Usage: {} seconds_to_run", args[0]);
        return;
    }

    let run_duration = Duration::from_secs(args[1].parse::<u64>().unwrap());

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(SERVER_ID)
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

    print!("Connecting to MQTT broker as {SERVER_ID} ... ");
    let session = Session::new(session_options).unwrap();
    println!("Connected!");

    let mqtt_client = session.create_managed_client();
    tokio::task::spawn(increment_execute_loop(mqtt_client.clone()));
    tokio::task::spawn(get_location_execute_loop(mqtt_client.clone()));

    print!("Starting server ... ");
    tokio::spawn(exit_timer(session.create_exit_handle(), run_duration));
    println!("server running for {run_duration:?}");

    session.run().await.unwrap();

    println!("Server stopped");
}

async fn increment_execute_loop(client: SessionManagedClient) {
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let options = CommandOptionsBuilder::default().build().unwrap();

    let mut increment_executor =
        IncrementCommandExecutor::new(application_context, client, &options);

    let mut counter_values: HashMap<String, i32> = HashMap::new();
    counter_values.insert("alpha".to_string(), 0);
    counter_values.insert("beta".to_string(), 0);

    loop {
        let request = increment_executor.recv().await.unwrap().unwrap();
        let mut response_builder = IncrementResponseBuilder::default();

        let response = match counter_values.get(&request.payload.counter_name) {
            Some(current_value) => {
                if *current_value < i32::MAX {
                    let new_value = *current_value + 1;
                    counter_values.insert(request.payload.counter_name.clone(), new_value);

                    response_builder
                        .payload(IncrementResponsePayload {
                            counter_value: new_value,
                        })
                        .unwrap()
                        .build()
                        .unwrap()
                } else {
                    response_builder
                        .error(CounterError {
                            condition: Some(ConditionSchema::CounterOverflow),
                            explanation: Some(format!(
                                "Rust counter '{}' has saturated; no further increment is possible",
                                &request.payload.counter_name
                            )),
                        })
                        .unwrap()
                        .build()
                        .unwrap()
                }
            }
            None => {
                response_builder
                    .error(CounterError {
                        condition: Some(ConditionSchema::CounterNotFound),
                        explanation: Some(format!(
                            "Rust counter '{}' not found in counter collection",
                            &request.payload.counter_name
                        )),
                    })
                    .unwrap()
                    .build()
                    .unwrap()
            }
        };

        request.complete(response).await.unwrap();
    }
}

async fn get_location_execute_loop(client: SessionManagedClient) {
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let options = CommandOptionsBuilder::default().build().unwrap();

    let mut get_location_executor =
        GetLocationCommandExecutor::new(application_context, client, &options);

    let mut counter_locations: HashMap<String, Option<CounterLocation>> = HashMap::new();
    counter_locations.insert("alpha".to_string(), Some(CounterLocation { latitude: 14.4, longitude: -123.0 }));
    counter_locations.insert("beta".to_string(), None);

    loop {
        let request = get_location_executor.recv().await.unwrap().unwrap();
        let mut response_builder = GetLocationResponseBuilder::default();

        let response = match counter_locations.get(&request.payload.counter_name) {
            Some(counter_location) => {
                response_builder
                    .payload(GetLocationResponsePayload {
                        counter_location: counter_location.clone(),
                    })
                    .unwrap()
                    .build()
                    .unwrap()
            }
            None => {
                response_builder
                    .error(CounterError {
                        condition: Some(ConditionSchema::CounterNotFound),
                        explanation: Some(format!(
                            "Rust counter '{}' not found in counter collection",
                            &request.payload.counter_name
                        )),
                    })
                    .unwrap()
                    .build()
                    .unwrap()
            }
        };

        request.complete(response).await.unwrap();
    }
}

async fn exit_timer(exit_handle: SessionExitHandle, exit_after: Duration) {
    tokio::time::sleep(exit_after).await;
    exit_handle.try_exit().await.unwrap();
}
