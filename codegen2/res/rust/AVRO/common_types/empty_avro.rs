/* This file will be copied into the folder for generated code. */

use std::io::Cursor;

use apache_avro;
use azure_iot_operations_protocol::common::payload_serialize::{
    DeserializationError, FormatIndicator, PayloadSerialize, SerializedPayload,
};
use lazy_static;
use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct EmptyAvro {
}

impl PayloadSerialize for EmptyAvro{
    type Error = String;

    fn serialize(self) -> Result<SerializedPayload, Self::Error> {
        Ok(SerializedPayload {
            payload: apache_avro::to_avro_datum(&SCHEMA, apache_avro::to_value(self).unwrap()).unwrap(),
            content_type: "application/avro".to_string(),
            format_indicator: FormatIndicator::UnspecifiedBytes,
        })
    }

    fn deserialize(
        payload: &[u8],
        _content_type: Option<&String>,
        _format_indicator: &FormatIndicator,
    ) -> Result<Self, DeserializationError<Self::Error>> {
        Ok(apache_avro::from_value(&apache_avro::from_avro_datum(&SCHEMA, &mut Cursor::new(payload), None).unwrap()).unwrap())
    }
}

lazy_static::lazy_static! { pub static ref SCHEMA: apache_avro::Schema = apache_avro::Schema::parse_str(RAW_SCHEMA).unwrap(); }

const RAW_SCHEMA: &str = r#"
{
  "namespace": "resources.common_types",
  "name": "EmptyAvro",
  "type": "record",
  "fields": []
}
"#;
