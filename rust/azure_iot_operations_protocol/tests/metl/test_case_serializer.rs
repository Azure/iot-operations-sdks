// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::marker::PhantomData;

use serde::Deserialize;

use crate::metl::default_serializer::DefaultSerializer;
use crate::metl::defaults::DefaultsType;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseSerializer<T: DefaultsType + Default> {
    #[serde(default)]
    pub defaults_type: PhantomData<T>,

    #[serde(rename = "out-content-type")]
    #[serde(default = "get_default_out_content_type::<T>")]
    pub out_content_type: Option<String>,

    #[serde(rename = "accept-content-types")]
    #[serde(default = "get_default_accept_content_types::<T>")]
    pub accept_content_types: Vec<String>,

    #[serde(rename = "indicate-character-data")]
    #[serde(default = "get_default_indicate_character_data::<T>")]
    pub indicate_character_data: bool,

    #[serde(rename = "allow-character-data")]
    #[serde(default = "get_default_allow_character_data::<T>")]
    pub allow_character_data: bool,

    #[serde(rename = "fail-deserialization")]
    #[serde(default = "get_default_fail_deserialization::<T>")]
    pub fail_deserialization: bool,
}

impl<T: DefaultsType + Default> TestCaseSerializer<T> {
    pub fn get_default() -> Self {
        if let Some(default_serializer) = get_default_serializer::<T>() {
            Self {
                defaults_type: PhantomData,
                out_content_type: default_serializer.out_content_type.clone(),
                accept_content_types: default_serializer
                    .accept_content_types
                    .clone()
                    .unwrap_or_default(),
                indicate_character_data: default_serializer
                    .indicate_character_data
                    .unwrap_or_default(),
                allow_character_data: default_serializer.allow_character_data.unwrap_or_default(),
                fail_deserialization: default_serializer.fail_deserialization.unwrap_or_default(),
            }
        } else {
            Self {
                defaults_type: PhantomData,
                out_content_type: None,
                accept_content_types: Vec::new(),
                indicate_character_data: false,
                allow_character_data: false,
                fail_deserialization: false,
            }
        }
    }
}

pub fn get_default_serializer<T: DefaultsType + Default>() -> Option<&'static DefaultSerializer> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_executor) = default_prologue.executor.as_ref() {
                return default_executor.serializer.as_ref();
            } else if let Some(default_invoker) = default_prologue.invoker.as_ref() {
                return default_invoker.serializer.as_ref();
            } else if let Some(default_receiver) = default_prologue.receiver.as_ref() {
                return default_receiver.serializer.as_ref();
            } else if let Some(default_sender) = default_prologue.sender.as_ref() {
                return default_sender.serializer.as_ref();
            }
        }
    }

    None
}

pub fn get_default_out_content_type<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_serializer) = get_default_serializer::<T>() {
        if let Some(default_out_content_type) = default_serializer.out_content_type.as_ref() {
            return Some(default_out_content_type.to_string());
        }
    }

    None
}

pub fn get_default_accept_content_types<T: DefaultsType + Default>() -> Vec<String> {
    if let Some(default_serializer) = get_default_serializer::<T>() {
        if let Some(default_accept_content_types) = default_serializer.accept_content_types.as_ref()
        {
            return (*default_accept_content_types).clone();
        }
    }

    Vec::new()
}

pub fn get_default_indicate_character_data<T: DefaultsType + Default>() -> bool {
    if let Some(default_serializer) = get_default_serializer::<T>() {
        if let Some(default_indicate_character_data) = default_serializer.indicate_character_data {
            return default_indicate_character_data;
        }
    }

    false
}

pub fn get_default_allow_character_data<T: DefaultsType + Default>() -> bool {
    if let Some(default_serializer) = get_default_serializer::<T>() {
        if let Some(default_allow_character_data) = default_serializer.allow_character_data {
            return default_allow_character_data;
        }
    }

    false
}

pub fn get_default_fail_deserialization<T: DefaultsType + Default>() -> bool {
    if let Some(default_serializer) = get_default_serializer::<T>() {
        if let Some(default_fail_deserialization) = default_serializer.fail_deserialization {
            return default_fail_deserialization;
        }
    }

    false
}
