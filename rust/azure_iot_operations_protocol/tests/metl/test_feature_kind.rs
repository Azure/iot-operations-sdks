// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

#[derive(Clone, Deserialize, Debug, PartialEq)]
pub enum TestFeatureKind {
    #[serde(rename = "unobtanium")]
    Unobtanium,

    #[serde(rename = "ack-ordering")]
    AckOrdering,

    #[serde(rename = "reconnection")]
    Reconnection,

    #[serde(rename = "caching")]
    Caching,

    #[serde(rename = "dispatch")]
    Dispatch,

    #[serde(rename = "explicit-default")]
    ExplicitDefault,
}
