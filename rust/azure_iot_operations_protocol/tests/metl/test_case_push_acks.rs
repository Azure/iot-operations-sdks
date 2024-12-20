// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

use crate::metl::test_ack_kind::TestAckKind;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCasePushAcks {
    #[serde(rename = "publish")]
    #[serde(default)]
    pub publish: Vec<TestAckKind>,

    #[serde(rename = "subscribe")]
    #[serde(default)]
    pub subscribe: Vec<TestAckKind>,

    #[serde(rename = "unsubscribe")]
    #[serde(default)]
    pub unsubscribe: Vec<TestAckKind>,
}
