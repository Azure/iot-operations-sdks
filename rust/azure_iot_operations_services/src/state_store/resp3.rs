// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types and serialization/deserialization implementations for RESP3 protocol.

use std::time::Duration;

use azure_iot_operations_protocol::common::{
    hybrid_logical_clock::HybridLogicalClock,
    payload_serialize::{FormatIndicator, PayloadSerialize},
};

#[derive(Clone, Debug)]
pub(crate) enum Request {
    /// `key`, `value`, options
    Set(Vec<u8>, Vec<u8>, SetOptions),
    /// `key`
    Get(Vec<u8>),
    /// `key`
    Del(Vec<u8>),
    /// `key`, `value`
    VDel(Vec<u8>, Vec<u8>),
}

/// Options for a `Set` Request
#[derive(Clone, Debug, Default)]
pub struct SetOptions {
    /// Condition for the `Set` operation
    pub set_condition: SetCondition,
    /// How long the key should persist before it expires, in millisecond precision.
    pub expires: Option<Duration>,
    /// Optional fencing token for the `Set` operation
    pub fencing_token: Option<HybridLogicalClock>,
}

/// Condition for a `Set` Request
#[derive(Clone, Debug, Default)]
pub enum SetCondition {
    /// The `Set` operation will only execute if the State Store does not have this key already.
    OnlyIfDoesNotExist,
    /// The `Set` operation will only execute if the State Store does not have this key or it has this key and
    /// the value in the State Store is equal to the value provided for this `Set` operation.
    OnlyIfEqualOrDoesNotExist,
    /// The `Set` operation will execute regardless of if the key exists already and regardless of the value
    /// of this key in the State Store.
    #[default]
    Unconditional,
}

impl PayloadSerialize for Request {
    type Error = String;
    fn content_type() -> &'static str {
        "application/octet-stream"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::UnspecifiedBytes
    }

    fn serialize(&self) -> Result<Vec<u8>, String> {
        Ok(match self {
            Request::Set(key, value, options) => serialize_set(key, value, options),
            Request::Get(key) => serialize_get(key),
            Request::Del(key) => serialize_del(key),
            Request::VDel(key, value) => serialize_v_del(key, value),
        })
    }

    fn deserialize(_payload: &[u8]) -> Result<Self, String> {
        Err("Not implemented".into())
    }
}

// ----------------------- Serialization Functions -----------------------

/// `RequestBufferBuilder` builds a RESP3 buffer for sending to the State Store.
struct RequestBufferBuilder {
    buffer: Vec<u8>,
}

impl RequestBufferBuilder {
    fn new() -> Self {
        RequestBufferBuilder { buffer: Vec::new() }
    }

    fn get_buffer(self) -> Vec<u8> {
        self.buffer
    }

    fn append_array_number(&mut self, num_elements: u32) {
        self.buffer
            .extend(format!("*{num_elements}\r\n").as_bytes());
    }

    fn append_argument(&mut self, arg: &[u8]) {
        self.buffer.extend(format!("${}\r\n", arg.len()).as_bytes());
        self.buffer.extend(arg);
        self.buffer.extend(b"\r\n");
    }
}

/// Determines number of additional arguments needed for RESP3 payload
fn get_number_additional_arguments(options: &SetOptions) -> u32 {
    let mut additional_arguments: u32 = 0;

    match options.set_condition {
        SetCondition::OnlyIfEqualOrDoesNotExist | SetCondition::OnlyIfDoesNotExist => {
            additional_arguments += 1;
        }
        SetCondition::Unconditional => (),
    }

    if options.expires.is_some() {
        additional_arguments += 2;
    }

    additional_arguments
}

/// Builds a RESP3 payload to `SET(key=value)`
fn serialize_set(key: &[u8], value: &[u8], options: &SetOptions) -> Vec<u8> {
    let mut builder = RequestBufferBuilder::new();

    let mut num_arguments = 3;

    num_arguments += get_number_additional_arguments(options);

    builder.append_array_number(num_arguments);
    builder.append_argument(b"SET");

    builder.append_argument(key);
    builder.append_argument(value);

    match options.set_condition {
        SetCondition::OnlyIfDoesNotExist => builder.append_argument(b"NX"),
        SetCondition::OnlyIfEqualOrDoesNotExist => builder.append_argument(b"NEX"),
        SetCondition::Unconditional => (),
    }

    if let Some(expires) = options.expires {
        builder.append_argument(b"PX");
        builder.append_argument(expires.as_millis().to_string().as_bytes());
    }

    builder.get_buffer()
}

/// Builds a RESP3 payload to `GET(key)`
fn serialize_get(key: &[u8]) -> Vec<u8> {
    let mut builder = RequestBufferBuilder::new();
    builder.append_array_number(2);
    builder.append_argument(b"GET");
    builder.append_argument(key);
    builder.get_buffer()
}

/// Builds a RESP3 payload to `DEL(key)`
fn serialize_del(key: &[u8]) -> Vec<u8> {
    let mut builder = RequestBufferBuilder::new();
    builder.append_array_number(2);
    builder.append_argument(b"DEL");
    builder.append_argument(key);
    builder.get_buffer()
}

/// Builds a RESP3 payload to `VDEL(key, value)`
fn serialize_v_del(key: &[u8], value: &[u8]) -> Vec<u8> {
    let mut builder = RequestBufferBuilder::new();
    builder.append_array_number(3);
    builder.append_argument(b"VDEL");
    builder.append_argument(key);
    builder.append_argument(value);
    builder.get_buffer()
}

// ----------------------- Response Types -----------------------

#[derive(Clone, Debug, PartialEq)]
pub(crate) enum Response {
    /// Successful `Set` response
    Ok,
    /// Successful `Get` response
    Value(Vec<u8>),
    /// Successful `Del` or `VDel`. Specifies the number of keys deleted
    ValuesDeleted(usize),
    // ProtocolError,
    /// Key not found for `Get`, `Del`, or `VDel` or parameters caused the operation to not be applied for `Set` or `VDel`
    NotFound,
    /// Description of error because of invalid request
    Error(Vec<u8>),
}

impl Response {
    const RESPONSE_OK: &'static [u8] = b"+OK\r\n";
    const RESPONSE_ERROR_PREFIX: &'static [u8] = b"-ERR ";
    const RESPONSE_SUFFIX: &'static [u8] = b"\r\n";
    const GET_RESPONSE_NOT_FOUND: &'static [u8] = b"$-1\r\n";
    const RESPONSE_SET_NOT_APPLIED: &'static [u8] = b":-1\r\n";
    const RESPONSE_KEY_NOT_FOUND: &'static [u8] = b":0\r\n";
    const RESPONSE_LENGTH_PREFIX: &'static [u8] = b"$";
    const DELETE_RESPONSE_PREFIX: &'static [u8] = b":";

    fn parse_error(payload: &[u8]) -> Result<Vec<u8>, String> {
        if let Some(err) = payload.strip_prefix(Self::RESPONSE_ERROR_PREFIX) {
            if let Some(err_msg) = err.strip_suffix(Self::RESPONSE_SUFFIX) {
                return Ok(err_msg.to_vec());
            }
        }
        Err(format!("Invalid error response: {payload:?}"))
    }
}

impl PayloadSerialize for Response {
    type Error = String;
    fn content_type() -> &'static str {
        "application/octet-stream"
    }

    fn format_indicator() -> FormatIndicator {
        FormatIndicator::UnspecifiedBytes
    }

    fn serialize(&self) -> Result<Vec<u8>, String> {
        Err("Not implemented".into())
    }

    fn deserialize(payload: &[u8]) -> Result<Self, String> {
        match payload {
            Self::RESPONSE_OK => Ok(Response::Ok),
            Self::GET_RESPONSE_NOT_FOUND
            | Self::RESPONSE_KEY_NOT_FOUND
            | Self::RESPONSE_SET_NOT_APPLIED => Ok(Response::NotFound),
            _ if payload.starts_with(Self::RESPONSE_ERROR_PREFIX) => {
                Ok(Response::Error(Self::parse_error(payload)?))
            }
            _ if payload.starts_with(Self::RESPONSE_LENGTH_PREFIX) => Ok(Response::Value(
                parse_value(payload, Self::RESPONSE_LENGTH_PREFIX)?,
            )),
            _ if payload.starts_with(Self::DELETE_RESPONSE_PREFIX) => Ok(Response::ValuesDeleted(
                parse_numeric(payload, Self::DELETE_RESPONSE_PREFIX)?,
            )),
            _ => Err(format!("Unknown response: {payload:?}")),
        }
    }
}

// ----------------------- DESERIALIZE FUNCTIONS -----------------------
const RESPONSE_SUFFIX: &[u8] = b"\r\n";

fn parse_numeric(payload: &[u8], prefix: &[u8]) -> Result<usize, String> {
    if let Some(val) = payload.strip_prefix(prefix) {
        let (num_deleted, current_index) = get_numeric(val)?;
        // after the number of deleted keys, there should be '\r\n' and then nothing else. '\r' is already verified in get_numeric
        if current_index + 2 == val.len() && val[current_index + 1] == b'\n' {
            return Ok(num_deleted);
        }
    }
    Err(format!("Invalid numeric response: {payload:?}"))
}

fn get_numeric(payload: &[u8]) -> Result<(usize, usize), String> {
    let mut value_len: usize = 0;
    let mut current_index: usize = 0;
    for byte in &payload[0..] {
        match byte {
            b'\r' => {
                break;
            }

            b'0'..=b'9' => {
                let value = usize::from(byte - b'0');

                match value_len.checked_mul(10) {
                    Some(v) => value_len = v,
                    None => {
                        return Err(format!(
                            "Multiplication overflow while parsing value length: {payload:?}"
                        ));
                    }
                }
                match value_len.checked_add(value) {
                    Some(v) => value_len = v,
                    None => {
                        return Err(format!(
                            "Addition overflow while parsing value length: {payload:?}"
                        ));
                    }
                }
            }

            _ => {
                return Err(format!("Invalid value length format: {payload:?}"));
            }
        }
        current_index += 1;
    }
    Ok((value_len, current_index))
}

/// For a response or notification that contains a value (embedded in extra
/// protocol overhead), return just the value itself.
/// E.G. for the string "$5\r\nABCDE\r\n", this will return
/// b"ABCDE".
/// Inputs to this should be the entire payload (for error purposes) and any prefix that is before the length of the value.
/// So a payload of "$5\r\nABCDE\r\n" should pass in b"$" as the prefix, and a payload
/// of "*4\r\n$6\r\nNOTIFY\r\n$3\r\nSET\r\n$5\r\nVALUE\r\n$3\r\nabc\r\n" should pass in b"*4\r\n$6\r\nNOTIFY\r\n$3\r\nSET\r\n$5\r\nVALUE\r\n$" as the prefix.
fn parse_value(payload: &[u8], prefix: &[u8]) -> Result<Vec<u8>, String> {
    if let Some(stripped_payload) = payload.strip_prefix(prefix) {
        // get length of value
        let (value_len, mut current_index) = get_numeric(stripped_payload)?;
        current_index += 1; // '\r' that triggered get_numeric to return
                            // '\n' should be next
        if current_index == stripped_payload.len() || stripped_payload[current_index] != b'\n' {
            return Err(format!("Invalid format: {payload:?}"));
        }
        current_index += 1;
        if current_index + value_len + 2 != stripped_payload.len() {
            return Err(format!(
                "Value length does not match actual value length: {payload:?}"
            ));
        }

        let closing_bytes =
            &stripped_payload[(stripped_payload.len() - 2)..(stripped_payload.len())];
        if closing_bytes != RESPONSE_SUFFIX {
            return Err(format!("Invalid format: {payload:?}"));
        }

        Ok(stripped_payload[current_index..current_index + value_len].to_vec())
    } else {
        Err(format!(
            "Invalid payload, must start with {prefix:?}: {payload:?}"
        ))
    }
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::*;

    // ------------- Deserialize Response Tests -------------

    #[test]
    fn test_set_response() {
        assert!(matches!(
            Response::deserialize(b"+OK\r\n").unwrap(),
            Response::Ok
        ));
    }

    #[test]
    fn test_did_not_set_response() {
        assert!(matches!(
            Response::deserialize(b":-1\r\n").unwrap(),
            Response::NotFound
        ));
    }

    #[test]
    fn test_get_response_success() {
        assert_eq!(
            Response::deserialize(b"$4\r\n1234\r\n").unwrap(),
            Response::Value(b"1234".to_vec())
        );
    }

    #[test]
    fn test_get_response_empty_success() {
        assert_eq!(
            Response::deserialize(b"$0\r\n\r\n").unwrap(),
            Response::Value(b"".to_vec())
        );
    }

    #[test]
    fn test_get_response_no_key() {
        assert!(matches!(
            Response::deserialize(b"$-1\r\n").unwrap(),
            Response::NotFound
        ));
    }

    // same as a vdel response
    #[test]
    fn test_del_response() {
        assert!(matches!(
            Response::deserialize(b":1\r\n").unwrap(),
            Response::ValuesDeleted(1)
        ));
    }

    #[test]
    fn test_vdel_no_match_response() {
        assert!(matches!(
            Response::deserialize(b":-1\r\n").unwrap(),
            Response::NotFound
        ));
    }

    // same as a vdel response
    // this isn't currently possible, but could be in the future
    #[test]
    fn test_del_multiple_response() {
        assert!(matches!(
            Response::deserialize(b":6\r\n").unwrap(),
            Response::ValuesDeleted(6)
        ));
    }

    // same as a vdel response
    #[test]
    fn test_del_no_key() {
        assert!(matches!(
            Response::deserialize(b":0\r\n").unwrap(),
            Response::NotFound
        ));
    }

    #[test]
    fn test_error_response() {
        assert_eq!(
            Response::deserialize(b"-ERR syntax error\r\n").unwrap(),
            Response::Error(b"syntax error".to_vec())
        );
    }

    #[test]
    fn test_empty_error_response_success() {
        assert_eq!(
            Response::deserialize(b"-ERR \r\n").unwrap(),
            Response::Error(b"".to_vec())
        );
    }

    #[test_case(b"1"; "too short")]
    #[test_case(b"11\r\nhello world\r\n"; "no $ on get response")]
    #[test_case(b"$11hello world\r\n"; "missing first newline")]
    #[test_case(b"$11\r\nhello world"; "missing second newline")]
    #[test_case(b"$not an integer\r\nhello world"; "length not an integer")]
    #[test_case(b"$11\r\nthis string is longer than 11 characters\r\n"; "length not accurate")]
    #[test_case(b"-ERR\r\n"; "Malformed error")]
    #[test_case(b"ERR description\r\n"; "Error missing minus")]
    #[test_case(b"-ERR description"; "Error missing newline")]
    #[test_case(b":"; "Delete response too short")]
    #[test_case(b"1234\r\n"; "Delete response doesn't start with colon")]
    #[test_case(b":1234"; "Delete response doesn't end with newline")]
    #[test_case(b":not an integer\r\n"; "Delete response value not integer")]
    #[test_case(b"+hello world\r\n"; "Incorrect OK value")]
    #[test_case(b"+"; "OK response too short")]
    #[test_case(b"OK\r\n"; "OK response doesn't start with plus sign")]
    #[test_case(b"+OK"; "OK response doesn't end with newline")]

    fn test_response_failures(payload: &[u8]) {
        assert!(Response::deserialize(payload).is_err());
    }

    // --------------- Internal Fns tests ---------------------
    #[test]
    fn test_parse_number() {
        assert_eq!(
            parse_numeric(b":1234\r\n", Response::DELETE_RESPONSE_PREFIX).unwrap(),
            1234
        );
    }

    // ---------------- Serialize Request tests -----------------------
    #[test_case(SetOptions::default(),
        b"*3\r\n$3\r\nSET\r\n$7\r\ntestkey\r\n$9\r\ntestvalue\r\n";
        "default")]
    #[test_case(SetOptions {set_condition: SetCondition::OnlyIfDoesNotExist, ..Default::default()},
        b"*4\r\n$3\r\nSET\r\n$7\r\ntestkey\r\n$9\r\ntestvalue\r\n$2\r\nNX\r\n";
        "OnlyIfDoesNotExist")]
    #[test_case(SetOptions {set_condition: SetCondition::OnlyIfEqualOrDoesNotExist, ..Default::default()},
        b"*4\r\n$3\r\nSET\r\n$7\r\ntestkey\r\n$9\r\ntestvalue\r\n$3\r\nNEX\r\n";
        "OnlyIfEqualOrDoesNotExist")]
    #[test_case(SetOptions {expires: Some(Duration::from_millis(10)), ..Default::default()},
        b"*5\r\n$3\r\nSET\r\n$7\r\ntestkey\r\n$9\r\ntestvalue\r\n$2\r\nPX\r\n$2\r\n10\r\n";
        "expires set")]
    #[test_case(SetOptions {fencing_token: Some(HybridLogicalClock::new()), ..Default::default()},
        b"*3\r\n$3\r\nSET\r\n$7\r\ntestkey\r\n$9\r\ntestvalue\r\n";
        "fencing token set")]
    fn test_serialize_set_options(set_options: SetOptions, expected: &[u8]) {
        assert_eq!(
            Request::serialize(&Request::Set(
                b"testkey".to_vec(),
                b"testvalue".to_vec(),
                set_options
            ))
            .unwrap(),
            expected
        );
    }

    #[test]
    fn test_serialize_empty_set() {
        assert_eq!(
            Request::serialize(&Request::Set(
                b"".to_vec(),
                b"".to_vec(),
                SetOptions::default()
            ))
            .unwrap(),
            b"*3\r\n$3\r\nSET\r\n$0\r\n\r\n$0\r\n\r\n"
        );
    }

    #[test]
    fn test_serialize_get() {
        assert_eq!(
            Request::serialize(&Request::Get(b"testkey".to_vec(),)).unwrap(),
            b"*2\r\n$3\r\nGET\r\n$7\r\ntestkey\r\n"
        );
    }

    #[test]
    fn test_serialize_del() {
        assert_eq!(
            Request::serialize(&Request::Del(b"testkey".to_vec(),)).unwrap(),
            b"*2\r\n$3\r\nDEL\r\n$7\r\ntestkey\r\n"
        );
    }

    #[test]
    fn test_serialize_vdel() {
        assert_eq!(
            Request::serialize(&Request::VDel(b"testkey".to_vec(), b"testvalue".to_vec(),))
                .unwrap(),
            b"*3\r\n$4\r\nVDEL\r\n$7\r\ntestkey\r\n$9\r\ntestvalue\r\n"
        );
    }
}
