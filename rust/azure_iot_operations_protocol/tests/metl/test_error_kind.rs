#[allow(dead_code)]
use serde::Deserialize;

#[derive(Clone, Deserialize, Debug, PartialEq)]
pub enum TestErrorKind {
    #[serde(rename = "none")]
    None,

    #[serde(rename = "content")]
    Content,

    #[serde(rename = "execution")]
    Execution,
}
