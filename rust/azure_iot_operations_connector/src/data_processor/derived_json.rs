// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Processor for transforming [`Data`] with a JSON payload according to transformation rules
//! defined in a [`Dataset`].

use std::collections::BTreeMap;

use azure_iot_operations_services::azure_device_registry::models::Dataset;
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
    SerdeError(#[from] serde_json::Error),
    #[error(transparent)]
    SchemaError(#[from] MessageSchemaBuilderError),
    #[error("Datapoints mapped multiple values to the same output field: {0}")]
    DuplicateField(String),
}

/// Transform the input [`Data`] according to the JSON transformation defined in the [`Dataset`].
/// Returns the transformed [`Data`] and a new [`MessageSchema`] that describes it.
///
/// # Limitations
/// - Cannot correctly interpret enums as it derives the schema only from JSON payload provided.
/// - Similarly, optionality of fields cannot be inferred correctly in the schema.
/// - Fields not present on the input JSON will be set to `null` in the output JSON if a transform
///   for them is defined in the dataset.
/// - Fields that are set to `null` as mentioned above will be set to `true` in the schema, as no
///   information is available to derive the type of the field.
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
    let input_json: Value = serde_json::from_slice(&data.payload)?;

    // Build a `BTreeMap` of output fields, derived from the input JSON and the datapoint
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

    let output_json = if dataset.data_points.is_empty() {
        // If there are no data points, we simply return the input JSON as the output
        input_json
    } else {
        // Create the output JSON from the `BTreeMap`
        // TODO: subject to change, this may be a point-in-time decision.
        // Verify, and consider refactor if this design stays around.
        serde_json::to_value(&output_btm)?
    };

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
    use azure_iot_operations_services::azure_device_registry::models::DatasetDataPoint;
    use test_case::test_case;

    struct TransformTestCase {
        dataset: Dataset,
        input_json: Value,
        expected_output_json: Value,
        expected_output_json_schema: Value,
    }

    macro_rules! create_dataset_from_transform {
        ($($data_point:expr),*) => {
            Dataset {
                dataset_configuration: None,
                data_points: vec![
                    $(
                        DatasetDataPoint {
                            name: String::from($data_point.0),
                            data_source: String::from($data_point.1),
                            type_ref: None,
                            data_point_configuration: None,
                        }
                    ),*
                ],
                data_source: None,
                destinations: vec![],
                name: "TestDataset".to_string(),
                type_ref: None,
            }
        };
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
    fn message_schema_eq(schema1: &MessageSchema, schema2: &MessageSchema) -> bool {
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

    /// Test case for 1:1 transformation of JSON values
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
                ],
                "coordinates": {
                    "latitude": 10.12,
                    "longitude": 20.17
                }
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
            ],
            "location": {
                "latitude": 10.12,
                "longitude": 20.17
            }
        }"#;
        let expected_output_json: Value = serde_json::from_str(expected_output_json_str).unwrap();

        let dataset = create_dataset_from_transform!(
            ("factory", "metadata.factory"),
            ("temperature", "temp"),
            ("active", "active"),
            ("days", "metadata.active_on"),
            ("location", "metadata.coordinates")
        );

        // Can derive string, boolean, integer, float, array and object types for the schema
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
                },
                "location": {
                    "type": "object",
                    "properties": {
                        "latitude": {
                            "type": "number"
                        },
                        "longitude": {
                            "type": "number"
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

        let dataset = create_dataset_from_transform!(
            ("factory", "metadata.factory"),
            ("temperature", "temp")
        );

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

        let dataset = create_dataset_from_transform!(
            ("factory", "metadata.factory"),
            ("temperature", "temp"),
            ("active", "active"),
            ("meta", "metadata")
        );

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

    // Test case containing no datapoints
    fn valid_testcase_4() -> TransformTestCase {
        let input_json_str = r#"{
            "factory": "home",
            "temp": 10
        }"#;
        let input_json: Value = serde_json::from_str(input_json_str).unwrap();

        // TODO: determine which is the correct output JSON to return for this case
        // let expected_output_json_str = r"{}";
        // let expected_output_json: Value = serde_json::from_str(expected_output_json_str).unwrap();

        let expected_output_json = input_json.clone();

        let dataset = create_dataset_from_transform!(
            // No datapoints in the dataset
        );

        // TODO: determine which is the correct schema to return for this case
        // // No properties on output schema
        // let expected_output_json_schema_str = r#"{
        //     "$schema": "http://json-schema.org/draft-07/schema#",
        //     "type": "object"
        // }"#;

        let expected_output_json_schema_str = r#"{
            "$schema": "http://json-schema.org/draft-07/schema#",
            "type": "object",
            "properties": {
                "factory": {
                    "type": "string"
                },
                "temp": {
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

    // Test case containing datapoints that correspond to values that are not in the input
    fn valid_testcase_5() -> TransformTestCase {
        let input_json_str = r#"{
            "factory": "home",
            "temp": 10
        }"#;
        let input_json: Value = serde_json::from_str(input_json_str).unwrap();

        let expected_output_json_str = r#"{
            "factory": "home",
            "temperature": 10,
            "metadata": null
        }"#;
        let expected_output_json: Value = serde_json::from_str(expected_output_json_str).unwrap();

        let dataset = create_dataset_from_transform!(
            ("factory", "factory"),
            ("temperature", "temp"),
            ("metadata", "metadata")
        );

        // Metadata being null is not enough information to derive type information about the field
        // and so it is simply inferred as being "true"
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
                "metadata": true
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
    #[test_case(&valid_testcase_4(); "No datapoints")]
    #[test_case(&valid_testcase_5(); "Missing data source")]
    fn valid_transform(test_case: &TransformTestCase) {
        let input_data = Data {
            payload: serde_json::to_vec(&test_case.input_json).unwrap(),
            content_type: "application/json".to_string(),
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
        assert!(message_schema_eq(
            &output_message_schema,
            &expected_output_message_schema
        ));
    }

    #[test_case("not json".as_bytes(); "Not JSON")]
    #[test_case(&[0x9c, 0xe5, 0x78]; "Not UTF8")]
    fn invalid_data_payload(invalid_payload: &[u8]) {
        // Use an arbitrary test case for the dataset, since the transformation will fail anyway
        // due to bad input JSON
        let test_case = valid_testcase_1();

        let input_data = Data {
            payload: invalid_payload.into(),
            content_type: "application/json".to_string(),
            custom_user_data: vec![],
            timestamp: None,
        };

        let r = transform(input_data.clone(), &test_case.dataset);
        assert!(r.is_err());
        assert_eq!(*r.unwrap_err().data, input_data);
    }

    // NOTE: This test could be extended to check for other invalid dataset cases,
    // if there are others
    #[test]
    fn invalid_dataset_duplicate_datapoints() {
        let input_json_str = r#"{
            "factory": "home",
            "id": 10
        }"#;
        let input_json: Value = serde_json::from_str(input_json_str).unwrap();
        let input_data = Data {
            payload: serde_json::to_vec(&input_json).unwrap(),
            content_type: "application/json".to_string(),
            custom_user_data: vec![],
            timestamp: None,
        };

        let dataset = create_dataset_from_transform!(("factory", "factory"), ("factory", "id"));

        let r = transform(input_data.clone(), &dataset);
        assert!(r.is_err());
        assert_eq!(*r.unwrap_err().data, input_data);
    }
}
