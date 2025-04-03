pub mod custom_payload;

use std::collections::HashMap;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::sync::{Arc, LazyLock};

use azure_iot_operations_mqtt::session::{
    Session, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_protocol::common::payload_serialize::FormatIndicator;
use azure_iot_operations_protocol::rpc_command;

use tokio::runtime::{Builder, Runtime};
use tokio::sync::Mutex;
use tokio::time::Duration;

use custom_payload::CustomPayload;

const MODEL_ID: &str = "dtmi:codegen:communicationTest:counterCollection;1";
const REQUEST_TOPIC_PATTERN: &str = "test/CounterCollection/{commandName}";
const CLIENT_ID: &str = "RustCounterClient";
const HOSTNAME: &str = "localhost";
const PORT: u16 = 1883;

static TOKIO_RUNTIME: LazyLock<Runtime> = LazyLock::new(|| {
    Builder::new_multi_thread()
        .worker_threads(8)
        .enable_all()
        .build()
        .expect("Tokio runtime builder failed to build multi-thread runtime")
});

static INC_INVOKER: LazyLock<Arc<Mutex<Option<rpc_command::Invoker<CustomPayload, CustomPayload, SessionManagedClient>>>>> = LazyLock::new(|| {Arc::new(Mutex::new(None))});

fn get_session() -> Session {
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
        .hostname(HOSTNAME)
        .tcp_port(PORT)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()
        .expect("MqttConnectionSettingsBuilder failed to build");

    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .expect("SessionOptionsBuilder failed to build");

    Session::new(session_options).expect("Session failed to construct")
}

fn create_invoker(session: &Session) {
    let application_context = ApplicationContextBuilder::default().build().expect("ApplicationContextBuilder failed to build");

    let mqtt_client = session.create_managed_client();

    let mut topic_token_map: HashMap<String, String> = HashMap::default();
    topic_token_map.insert("modelId".to_string(), MODEL_ID.to_string());
    topic_token_map.insert("invokerClientId".to_string(), CLIENT_ID.to_string());
    topic_token_map.insert("commandName".to_string(), "increment".to_string());

    let mut invoker_options_builder = rpc_command::invoker::OptionsBuilder::default();
    let invoker_options = invoker_options_builder
        .request_topic_pattern(REQUEST_TOPIC_PATTERN)
        .command_name("increment")
        .topic_token_map(topic_token_map)
        .build()
        .expect("invoker::OptionsBuilder failed to build");

    let mut invoker_singleton = TOKIO_RUNTIME.block_on(INC_INVOKER.lock());
    let invoker = TOKIO_RUNTIME.block_on(async move { rpc_command::Invoker::new(application_context, mqtt_client, invoker_options) });
    *invoker_singleton = Some(invoker.expect("Invoker failed to construct"));
}

#[unsafe(no_mangle)]
pub extern "C" fn init_sys() {
    let session = get_session();
    create_invoker(&session);
    let _ = TOKIO_RUNTIME.block_on(session.run());
}

#[unsafe(no_mangle)]
pub extern "C" fn invoke(req_buf: *const c_char, callback: extern "C" fn(*const c_char)) {
    let req_str: &CStr = unsafe { CStr::from_ptr(req_buf) };
    let req_slice: &str = req_str.to_str().unwrap();

    let payload = CustomPayload {
        payload: req_slice.as_bytes().to_vec(),
        content_type: "application/json".to_string(),
        format_indicator: FormatIndicator::Utf8EncodedCharacterData,
    };

    let request = rpc_command::invoker::RequestBuilder::default()
        .payload(payload)
        .expect("RequestBuilder unhappy with payload")
        .timeout(Duration::from_secs(10))
        .build()
        .expect("RequestBuilder failed to build");

    let invoker = INC_INVOKER.clone();
    TOKIO_RUNTIME.spawn(async move {
        let invoker = invoker.lock().await;
        if let Some(invoker) = &*invoker {
            let response = invoker.invoke(request).await;
            let resp_slice = response.expect("invoke returned None response").payload.payload;
            let resp_str = CString::new(resp_slice).unwrap();
            callback(resp_str.as_ptr());
        }
    });
}
