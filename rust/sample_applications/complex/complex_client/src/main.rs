// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::env;
use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use complex_envoy::common_types::common_options::{CommandOptionsBuilder, TelemetryOptionsBuilder};
use complex_envoy::dtmi_example_Complex__1::client::{
    Enum_Test_Day__1, Enum_Test_Result__1, GetTemperaturesCommandInvoker,
    GetTemperaturesRequestBuilder, GetTemperaturesRequestPayload, Object_GetTemperatures_Request,
    TelemetryCollectionReceiver,
};

#[tokio::main(flavor = "current_thread")]
async fn main() {
    // Initialize the logger
    env_logger::Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a session with connection settings from the environment
    let connection_settings = MqttConnectionSettingsBuilder::from_environment()
        .clean_start(true)
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let mut session = Session::new(session_options).unwrap();

    let jh = tokio::task::spawn(get_temperatures_and_receive(
        session.create_managed_client(),
        session.create_exit_handle(),
    ));

    tokio::task::spawn({
        async move {
            tokio::select! {
                _ = jh => {}
                () = tokio::time::sleep(Duration::from_secs(60)) => {

                }
            }
        }
    });

    // Run the session
    session.run().await.unwrap();
}

async fn get_temperatures_and_receive(
    client: SessionManagedClient,
    exit_handle: SessionExitHandle,
) {
    // Create invoker and receiver
    let get_temperatures_invoker = GetTemperaturesCommandInvoker::new(
        client.clone(),
        &CommandOptionsBuilder::default().build().unwrap(),
    );
    let mut temperature_map_receiver = TelemetryCollectionReceiver::new(
        client,
        &TelemetryOptionsBuilder::default().build().unwrap(),
    );

    // Request temperatures for tomorrow
    let request = GetTemperaturesRequestBuilder::default()
        .timeout(Duration::from_secs(10))
        .executor_id(env::var("COMPLEX_SERVER_ID").unwrap())
        .payload(GetTemperaturesRequestPayload {
            request: Object_GetTemperatures_Request {
                cities: Some(vec!["Seattle".to_string(), "Portland".to_string()]),
                when: Some(Enum_Test_Day__1::Tomorrow),
            },
        })
        .unwrap()
        .build()
        .unwrap();
    let response = get_temperatures_invoker.invoke(request).await.unwrap();

    // Validate command response
    assert_eq!(response.payload.response.len(), 2);
    let seattle_entry = response
        .payload
        .response
        .iter()
        .find(|e| e.city == Some("Seattle".to_string()))
        .unwrap();
    let portland_entry = response
        .payload
        .response
        .iter()
        .find(|e| e.city == Some("Portland".to_string()))
        .unwrap();
    assert_eq!(seattle_entry.temperature.unwrap(), -5.6);
    matches!(seattle_entry.result, Some(Enum_Test_Result__1::Success));

    assert_eq!(portland_entry.temperature.unwrap(), -13.2);
    matches!(portland_entry.result, Some(Enum_Test_Result__1::Success));

    // Receive telemetry
    let (telemetry, _) = temperature_map_receiver.recv().await.unwrap().unwrap();

    // Validate telemetry
    let temperatures = telemetry.payload.temperatures.unwrap();
    assert_eq!(temperatures.len(), 4);
    assert_eq!(*temperatures.get("Seattle").unwrap(), -2.22);
    assert_eq!(*temperatures.get("Los Angeles").unwrap(), 11.67);
    assert_eq!(*temperatures.get("Boston").unwrap(), -12.22);
    assert_eq!(*temperatures.get("San Francisco").unwrap(), -11.67);

    // Cleanup
    temperature_map_receiver.shutdown().await.unwrap();
    get_temperatures_invoker.shutdown().await.unwrap();
    exit_handle.try_exit().await.unwrap();
}
