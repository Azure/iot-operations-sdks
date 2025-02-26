// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;

#[derive(Deserialize, Debug)]
pub struct DefaultSerializer {
    #[serde(rename = "out-content-type")]
    pub out_content_type: Option<String>,

    #[serde(rename = "accept-content-types")]
    pub accept_content_types: Option<Vec<String>>,

    #[serde(rename = "indicate-character-data")]
    pub indicate_character_data: Option<bool>,

    #[serde(rename = "allow-character-data")]
    pub allow_character_data: Option<bool>,

    #[serde(rename = "fail-deserialization")]
    pub fail_deserialization: Option<bool>,
}
