// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
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
    pub data: Data,
}

/// Inner representation of a TransformError.
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
pub fn transform(mut data: Data, dataset: &Dataset) -> Result<(Data, MessageSchema), TransformError> {
    // NOTE: We delegate to a function here that modifies the data in place so that the entire
    // `data` struct does not need to be reallocated, while also being able to return it as part
    // of an error if necessary.
    match transform_in_place_and_create_output_schema(&mut data, dataset) {
        Ok(message_schema) => Ok((data, message_schema)),
        Err(e) => Err(TransformError {
            repr: e,
            data,
        }),
    }
}

/// Transform the input data in place according to the transformation defined in the dataset.
/// Returns a new MessageSchema that describes the transformed data.
/// 
/// Returns an error if the transformation or schema generation cannot be made. Input data will not be modified.
fn transform_in_place_and_create_output_schema(data: &mut Data, dataset: &Dataset) -> Result<MessageSchema, TransformErrorRepr> {
    // Parse the input JSON from bytes
    let input_json: Value = serde_json::from_str(std::str::from_utf8(&data.payload)?)?;

    // Build a `BTreeMap`` of output fields, derived from the input JSON and the datapoint
    // transformations defined in the dataset.
    let mut output_btm = BTreeMap::new();
    for (output_field, input_source) in dataset
        .data_points
        .iter()
        .map(|dp| {
            (&dp.name, &dp.data_source)
        })
    {
        // Use JMESPath to extract the value from the input JSON using the data source path defined
        // in the `DataPoint`, and add it to the output `BTreeMap`
        let v = jmespath::compile(input_source)?.search(&input_json)?;
        if let Some(_) = output_btm.insert(output_field.to_string(), v) {
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

    fn testcase_1() {
        let input_json_str = r#"{
            "metadata": {
                "factory": "home"
            },
            "temp": 10
        }"#;

        let expected_output_json_str = r#"{
            "factory": "home",
            "temperature": 10
        }"#;

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
            name: "TestDataset".to_string(),
            type_ref: None,
        };

        let expected_output_schema = 

    }

}