// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::{Deserialize, Deserializer};

#[allow(clippy::option_option)]
pub fn deserialize_optional_field<'de, T, D>(deserializer: D) -> Result<Option<Option<T>>, D::Error>
where
    D: Deserializer<'de>,
    T: Deserialize<'de>,
{
    Ok(Some(Option::deserialize(deserializer)?))
}
