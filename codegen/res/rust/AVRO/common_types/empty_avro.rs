/* This file will be copied into the folder for generated code. */

use std::io::Cursor;

use apache_avro;
use azure_iot_operations_protocol::common::payload_serialize::{FormatIndicator, PayloadSerialize};
use lazy_static;
use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct EmptyAvro {
}

impl PayloadSerialize for EmptyAvro{
    type Error = String;

    fn content_type() -> &'static str {
        "application/avro"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::UnspecifiedBytes
    }

    fn serialize(&self) -> Result<Vec<u8>, Self::Error> {
        Ok(apache_avro::to_avro_datum(&SCHEMA, apache_avro::to_value(self).unwrap()).unwrap())
    }

    fn deserialize(payload: &[u8]) -> Result<Self, Self::Error> {
        Ok(apache_avro::from_value(&apache_avro::from_avro_datum(&SCHEMA, &mut Cursor::new(payload), None).unwrap()).unwrap())
    }
}

lazy_static::lazy_static! { pub static ref SCHEMA: apache_avro::Schema = apache_avro::Schema::parse_str(RAW_SCHEMA).unwrap(); }

const RAW_SCHEMA: &str = r#"
{
  "namespace": "resources::AVRO::common_types::empty_avro",
  "name": "EmptyAvro",
  "type": "record",
  "fields": []
}
"#;
