// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Processor for transforming [`Data`] with a JSON payload according to transformation rules
//! defined in a [`Dataset`].

use std::collections::BTreeMap;

use azure_iot_operations_services::azure_device_registry::Dataset;
use azure_iot_operations_services::schema_registry::{Format, SchemaType};
use jmespath::{self, JmespathError};
use serde_json::{self, Value};

use crate::{Data, MessageSchema, MessageSchemaBuilder, MessageSchemaBuilderError};

/// An error that occurred during the transformation of data.
#[derive(Debug, thiserror::Error)]
#[error("{repr}")]
pub struct TransformError {
    #[source]
    repr: TransformErrorRepr,
    /// The data that was being transformed when the error occurred.
    pub data: Box<Data>, // NOTE: Use a Box to avoid large stack data in error
}

/// Inner representation of a [`TransformError`].
#[derive(Debug, thiserror::Error)]
enum TransformErrorRepr {
    #[error(transparent)]
    JmespathError(#[from] JmespathError),
    #[error(transparent)]
    Utf8Error(#[from] std::str::Utf8Error),
    #[error(transparent)]
    SerdeError(#[from] serde_json::Error),
    #[error(transparent)]
    SchemaError(#[from] MessageSchemaBuilderError),
    #[error("Datapoints mapped multiple values to the same output field: {0}")]
    DuplicateField(String),
}

/// Transform the input [`Data`] according to the JSON transformation defined in the [`Dataset`].
/// Returns the transformed [`Data`] and a new [`MessageSchema`] that describes it.
///
/// # Errors
/// Returns a [`TransformError`] if there is an error during the transformation or schema generation.
pub fn transform(
    mut data: Data,
    dataset: &Dataset,
) -> Result<(Data, MessageSchema), TransformError> {
    // NOTE: We delegate to a function here that modifies the data in place so that the entire
    // `data` struct does not need to be reallocated, while also being able to return it as part
    // of an error if necessary.
    match transform_in_place_and_create_output_schema(&mut data, dataset) {
        Ok(message_schema) => Ok((data, message_schema)),
        Err(e) => Err(TransformError {
            repr: e,
            data: Box::new(data),
        }),
    }
}

/// Transform the input data in place according to the transformation defined in the dataset.
/// Returns a new [`MessageSchema`] that describes the transformed data.
///
/// Returns an error if the transformation or schema generation cannot be made.
/// Input data will not be modified.
fn transform_in_place_and_create_output_schema(
    data: &mut Data,
    dataset: &Dataset,
) -> Result<MessageSchema, TransformErrorRepr> {
    // Parse the input JSON from bytes
    let input_json: Value = serde_json::from_str(std::str::from_utf8(&data.payload)?)?;

    // Build a `BTreeMap`` of output fields, derived from the input JSON and the datapoint
    // transformations defined in the dataset.
    let mut output_btm = BTreeMap::new();
    for (output_field, input_source) in dataset
        .data_points
        .iter()
        .map(|dp| (&dp.name, &dp.data_source))
    {
        // Use JMESPath to extract the value from the input JSON using the data source path defined
        // in the `DataPoint`, and add it to the output `BTreeMap`
        let v = jmespath::compile(input_source)?.search(&input_json)?;
        // Validate the inserted key did not already exist in the map
        if output_btm.insert(output_field.to_string(), v).is_some() {
            return Err(TransformErrorRepr::DuplicateField(output_field.to_string()));
        }
    }
    // Create the output JSON from the `BTreeMap`
    let output_json = serde_json::to_value(&output_btm)?;

    // Derive the schema from the output JSON, removing the unnecessary examples metadata
    let mut output_root_schema = schemars::schema_for_value!(&output_json);
    if let Some(ref mut metadata) = output_root_schema.schema.metadata {
        metadata.examples = vec![];
    }

    // Create a MessageSchema from the output JSON schema
    let output_message_schema = MessageSchemaBuilder::default()
        .content(serde_json::to_string(&output_root_schema)?)
        .format(Format::JsonSchemaDraft07)
        .schema_type(SchemaType::MessageSchema)
        .build()?;

    // Modify the input data struct to include the output JSON as the new payload
    // NOTE: Because we are modifying the data in place, there should be NO ERRORS after this point.
    data.payload = serde_json::to_vec(&output_json)?;

    Ok(output_message_schema)
}

#[cfg(test)]
mod test {
    use super::*;
    use azure_iot_operations_services::azure_device_registry::DatasetDataPoint;
    use test_case::test_case;

    struct TransformTestCase {
        dataset: Dataset,
        input_json: Value,
        expected_output_json: Value,
        expected_output_json_schema: Value,
    }

    /// Helper function to compare two `Data` structs for equality.
    /// This is necessary over the PartialEq/Eq trait because when using JSON, we can
    /// end up with different ordering of the keys in the JSON object, which prevents
    /// us from being able to make accurate comparisons of the `payload` field.
    fn json_data_eq(data1: &Data, data2: &Data) -> bool {
        // Make new structs with the payload set to empty vectors to normalize our Data under
        // comparison since we can't directly compare the payloads accurately.
        let data1_no_payload = Data {
            payload: Vec::new(),
            ..data1.clone()
        };
        let data2_no_payload = Data {
            payload: Vec::new(),
            ..data2.clone()
        };

        // Deserialize the JSON payloads
        let data1_json_payload: Value = serde_json::from_slice(&data1.payload).unwrap();
        let data2_json_payload: Value = serde_json::from_slice(&data2.payload).unwrap();

        // Now compare
        data1_no_payload == data2_no_payload && data1_json_payload == data2_json_payload
    }

    /// Helper function to compare two `MessageSchema` structs for equality.
    /// This is necessary over the PartialEq/Eq trait because when using JSON, we can
    /// end up with different ordering of the keys in the JSON object, which prevents
    /// us from being able to make accurate comparisons of the `content` field.
    fn message_schmea_eq(schema1: &MessageSchema, schema2: &MessageSchema) -> bool {
        // Make new structs with the content set to empty strings to normalize our MessageSchema
        // under comparison since we can't directly compare the content accurately.
        let schema1_no_content = MessageSchema {
            content: String::new(),
            ..schema1.clone()
        };
        let schema2_no_content = MessageSchema {
            content: String::new(),
            ..schema2.clone()
        };

        // Compare the content of the schemas
        let schema1_json_content: Value = serde_json::from_str(&schema1.content).unwrap();
        let schema2_json_content: Value = serde_json::from_str(&schema2.content).unwrap();

        schema1_no_content == schema2_no_content && schema1_json_content == schema2_json_content
    }

    /// Test case for 1:1 transformation of simple values
    fn valid_testcase_1() -> TransformTestCase {
        let input_json_str = r#"{
            "metadata": {
                "factory": "home",
                "active_on": [
                    "Monday",
                    "Tuesday",
                    "Wednesday",
                    "Thursday",
                    "Friday"
                ]
            },
            "temp": 10,
            "active": true
        }"#;
        let input_json: Value = serde_json::from_str(input_json_str).unwrap();

        let expected_output_json_str = r#"{
            "factory": "home",
            "temperature": 10,
            "active": true,
            "days": [
                "Monday",
                "Tuesday",
                "Wednesday",
                "Thursday",
                "Friday"
            ]
        }"#;
        let expected_output_json: Value = serde_json::from_str(expected_output_json_str).unwrap();

        let dataset = Dataset {
            dataset_configuration: None,
            data_points: vec![
                // This datapoint has a source in a different scope
                DatasetDataPoint {
                    name: String::from("factory"),
                    data_source: String::from("metadata.factory"),
                    type_ref: None,
                    data_point_configuration: None,
                },
                // This datapoint is renamed in the same scope
                DatasetDataPoint {
                    name: String::from("temperature"),
                    data_source: String::from("temp"),
                    type_ref: None,
                    data_point_configuration: None,
                },
                // This datapoint is unchanged in the transformation
                DatasetDataPoint {
                    name: String::from("active"),
                    data_source: String::from("active"),
                    type_ref: None,
                    data_point_configuration: None,
                },
                // This datapoint is renamed and found in a different scope
                DatasetDataPoint {
                    name: String::from("days"),
                    data_source: String::from("metadata.active_on"),
                    type_ref: None,
                    data_point_configuration: None,
                },
            ],
            data_source: None,
            destinations: vec![],
            name: "TestDataset".to_string(),
            type_ref: None,
        };

        // Can derive string, boolean, integer and array types for the schema
        let expected_output_json_schema_str = r#"{
            "$schema": "http://json-schema.org/draft-07/schema#",
            "type": "object",
            "properties": {
                "factory": {
                    "type": "string"
                },
                "temperature": {
                    "type": "integer"
                },
                "active": {
                    "type": "boolean"
                },
                "days": {
                    "type": "array",
                    "items": {
                        "type": "string"
                    }
                }
            }
        }"#;
        let expected_output_json_schema: Value =
            serde_json::from_str(expected_output_json_schema_str).unwrap();

        TransformTestCase {
            dataset,
            input_json,
            expected_output_json,
            expected_output_json_schema,
        }
    }

    // Test case for transforming only a subset of values
    fn valid_testcase_2() -> TransformTestCase {
        let input_json_str = r#"{
            "metadata": {
                "factory": "home",
                "active_on": [
                    "Monday",
                    "Tuesday",
                    "Wednesday",
                    "Thursday",
                    "Friday"
                ]
            },
            "temp": 10,
            "active": true
        }"#;
        let input_json: Value = serde_json::from_str(input_json_str).unwrap();

        let expected_output_json_str = r#"{
            "factory": "home",
            "temperature": 10
        }"#;
        let expected_output_json: Value = serde_json::from_str(expected_output_json_str).unwrap();

        let dataset = Dataset {
            dataset_configuration: None,
            // Not all input data is used in the transformation
            data_points: vec![
                // This datapoint has a source in a different scope
                DatasetDataPoint {
                    name: String::from("factory"),
                    data_source: String::from("metadata.factory"),
                    type_ref: None,
                    data_point_configuration: None,
                },
                // This datapoint is renamed in the same scope
                DatasetDataPoint {
                    name: String::from("temperature"),
                    data_source: String::from("temp"),
                    type_ref: None,
                    data_point_configuration: None,
                },
            ],
            data_source: None,
            destinations: vec![],
            name: "TestDataset".to_string(),
            type_ref: None,
        };

        let expected_output_json_schema_str = r#"{
            "$schema": "http://json-schema.org/draft-07/schema#",
            "type": "object",
            "properties": {
                "factory": {
                    "type": "string"
                },
                "temperature": {
                    "type": "integer"
                }
            }
        }"#;
        let expected_output_json_schema: Value =
            serde_json::from_str(expected_output_json_schema_str).unwrap();

        TransformTestCase {
            dataset,
            input_json,
            expected_output_json,
            expected_output_json_schema,
        }
    }

    /// Test case for transformation that involves overlapping values
    fn valid_testcase_3() -> TransformTestCase {
        let input_json_str = r#"{
            "metadata": {
                "factory": "home",
                "active_on": [
                    "Monday",
                    "Tuesday",
                    "Wednesday",
                    "Thursday",
                    "Friday"
                ]
            },
            "temp": 10,
            "active": true
        }"#;
        let input_json: Value = serde_json::from_str(input_json_str).unwrap();

        let expected_output_json_str = r#"{
            "factory": "home",
            "temperature": 10,
            "active": true,
            "meta": {
                "factory": "home",
                "active_on": [
                    "Monday",
                    "Tuesday",
                    "Wednesday",
                    "Thursday",
                    "Friday"
                ]
            }
        }"#;
        let expected_output_json: Value = serde_json::from_str(expected_output_json_str).unwrap();

        let dataset = Dataset {
            dataset_configuration: None,
            data_points: vec![
                // This datapoint has a source in a different scope
                DatasetDataPoint {
                    name: String::from("factory"),
                    data_source: String::from("metadata.factory"),
                    type_ref: None,
                    data_point_configuration: None,
                },
                // This datapoint is renamed in the same scope
                DatasetDataPoint {
                    name: String::from("temperature"),
                    data_source: String::from("temp"),
                    type_ref: None,
                    data_point_configuration: None,
                },
                // This datapoint is unchanged in the transformation
                DatasetDataPoint {
                    name: String::from("active"),
                    data_source: String::from("active"),
                    type_ref: None,
                    data_point_configuration: None,
                },
                // This datapoint is an object from which another datapoint was already derived
                DatasetDataPoint {
                    name: String::from("meta"),
                    data_source: String::from("metadata"),
                    type_ref: None,
                    data_point_configuration: None,
                },
            ],
            data_source: None,
            destinations: vec![],
            name: "TestDataset".to_string(),
            type_ref: None,
        };

        // Can derive string, boolean, integer and array types for the schema
        let expected_output_json_schema_str = r#"{
            "$schema": "http://json-schema.org/draft-07/schema#",
            "type": "object",
            "properties": {
                "factory": {
                    "type": "string"
                },
                "temperature": {
                    "type": "integer"
                },
                "active": {
                    "type": "boolean"
                },
                "meta": {
                    "type": "object",
                    "properties": {
                        "factory": {
                            "type": "string"
                        },
                        "active_on": {
                            "type": "array",
                            "items": {
                                "type": "string"
                            }
                        }
                    }
                }
            }
        }"#;
        let expected_output_json_schema: Value =
            serde_json::from_str(expected_output_json_schema_str).unwrap();

        TransformTestCase {
            dataset,
            input_json,
            expected_output_json,
            expected_output_json_schema,
        }
    }

    #[test_case(&valid_testcase_1(); "1:1 transformation")]
    #[test_case(&valid_testcase_2(); "Subset transformation")]
    #[test_case(&valid_testcase_3(); "Overlapping transformation")]
    fn valid_transform(test_case: &TransformTestCase) {
        let input_data = Data {
            payload: serde_json::to_vec(&test_case.input_json).unwrap(),
            content_type: None,
            custom_user_data: vec![],
            timestamp: None,
        };

        // We expect the output data to be the same as the input data, except for the payload
        // which contains the expected transformed output data
        let expected_output_data = Data {
            payload: serde_json::to_vec(&test_case.expected_output_json).unwrap(),
            ..input_data.clone()
        };

        // We expect the output message schema to contain the expected output JSON schema
        // and have the correct format and schema type
        let expected_output_message_schema = MessageSchemaBuilder::default()
            .content(serde_json::to_string(&test_case.expected_output_json_schema).unwrap())
            .format(Format::JsonSchemaDraft07)
            .schema_type(SchemaType::MessageSchema)
            .build()
            .unwrap();

        let (output_data, output_message_schema) =
            transform(input_data, &test_case.dataset).unwrap();

        assert!(json_data_eq(&output_data, &expected_output_data));
        assert!(message_schmea_eq(
            &output_message_schema,
            &expected_output_message_schema
        ));
    }
}
