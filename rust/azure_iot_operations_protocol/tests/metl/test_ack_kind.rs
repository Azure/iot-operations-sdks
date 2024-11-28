use serde::Deserialize;

#[derive(Clone, Deserialize, Debug, PartialEq)]
pub enum TestAckKind {
    #[serde(rename = "success")]
    Success,

    #[serde(rename = "fail")]
    Fail,

    #[serde(rename = "drop")]
    Drop,
}
