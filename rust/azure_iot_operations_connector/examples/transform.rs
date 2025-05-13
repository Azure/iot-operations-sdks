use serde_json::Value;
use schemars::{self, JsonSchema};
use azure_iot_operations_connector::data_processor::derived_json;
use azure_iot_operations_connector::Data;
use azure_iot_operations_services::azure_device_registry::{Dataset, DatasetDataPoint};

use std::collections::BTreeMap;

const INPUT: &str = r#"{
    "metadata": {
        "factory": "home"
    },
    "temp": 10
}"#;

const EXPECTED_OUTPUT: &str = r#"{
    "factory": "home",
    "temperature": 10
}"#;


fn main() {
    let dataset = Dataset {
        dataset_configuration: None,
        data_points: vec![
            DatasetDataPoint {
                name: String::from("factory"),
                data_source: String::from("metadata.factory"),
                type_ref: None,
                data_point_configuration: None,
            },
            DatasetDataPoint {
                name: String::from("temperature"),
                data_source: String::from("temp"),
                type_ref: None,
                data_point_configuration: None,
            },
        ],
    data_source: None,
        destinations: vec![],
        name: "MyDataset".to_string(),
        type_ref: None,
    };

    let data = Data {
        payload: INPUT.as_bytes().to_vec(),
        content_type: None,
        custom_user_data: vec![],
        timestamp: None,
    };

    let (data, schema) = derived_json::transform(data, &dataset).unwrap();

    println!("{schema:?}");

}