// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Processor for generating [`MessageSchema`] for the JSON payload defined in a [`Data`].

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
    Jmespath(#[from] JmespathError),
    #[error(transparent)]
    Serde(#[from] serde_json::Error),
    #[error(transparent)]
    Schema(#[from] MessageSchemaBuilderError),
}

/// Returns a new [`MessageSchema`] that describes it.
///
/// # Limitations
/// - Cannot correctly interpret enums as it derives the schema only from JSON payload provided.
/// - Similarly, optionality of fields cannot be inferred correctly in the schema.
/// - Fields that are set to `null` in the input JSON will be set to `true` in the schema, as no
///   information is available to derive the type of the field.
///
/// # Errors
/// Returns a [`TransformError`] if there is an error during the transformation or schema generation.
pub fn transform(data: Data) -> Result<MessageSchema, TransformError> {
    // NOTE: We delegate to a function here that modifies the data in place so that the entire
    // `data` struct does not need to be reallocated, while also being able to return it as part
    // of an error if necessary.
    match create_output_schema(&data) {
        Ok(message_schema) => Ok(message_schema),
        Err(e) => Err(TransformError {
            repr: e,
            data: Box::new(data),
        }),
    }
}

/// Generates a new [`MessageSchema`] that describes the data.
///
/// Returns an error if the transformation or schema generation cannot be made.
/// Input data will not be modified.
fn create_output_schema(data: &Data) -> Result<MessageSchema, TransformErrorRepr> {
    // Parse the input JSON from bytes
    let output_json: Value = serde_json::from_slice(&data.payload)?;

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

    Ok(output_message_schema)
}

#[cfg(test)]
mod test {
    use super::*;
    use test_case::test_case;

    struct TransformTestCase {
        input_json: Value,
        expected_output_json_schema: Value,
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
            input_json,
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
            input_json,
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
            input_json,
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
            input_json,
            expected_output_json_schema,
        }
    }

    #[test_case(&valid_testcase_1(); "1:1 transformation")]
    #[test_case(&valid_testcase_2(); "Subset transformation")]
    #[test_case(&valid_testcase_3(); "Overlapping transformation")]
    #[test_case(&valid_testcase_5(); "Missing data source")]
    fn valid_transform(test_case: &TransformTestCase) {
        let input_data = Data {
            payload: serde_json::to_vec(&test_case.input_json).unwrap(),
            content_type: "application/json".to_string(),
            custom_user_data: vec![],
            timestamp: None,
        };

        // We expect the output message schema to contain the expected output JSON schema
        // and have the correct format and schema type
        let expected_output_message_schema = MessageSchemaBuilder::default()
            .content(serde_json::to_string(&test_case.expected_output_json_schema).unwrap())
            .format(Format::JsonSchemaDraft07)
            .schema_type(SchemaType::MessageSchema)
            .build()
            .unwrap();

        let output_message_schema = transform(input_data).unwrap();

        assert!(message_schema_eq(
            &output_message_schema,
            &expected_output_message_schema
        ));
    }

    #[test_case("not json".as_bytes(); "Not JSON")]
    #[test_case(&[0x9c, 0xe5, 0x78]; "Not UTF8")]
    fn invalid_data_payload(invalid_payload: &[u8]) {
        let input_data = Data {
            payload: invalid_payload.into(),
            content_type: "application/json".to_string(),
            custom_user_data: vec![],
            timestamp: None,
        };

        let r = transform(input_data.clone());
        assert!(r.is_err());
        assert_eq!(*r.unwrap_err().data, input_data);
    }
}
