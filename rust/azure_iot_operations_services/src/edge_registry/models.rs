// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Models for Edge Registry operations.

use std::collections::HashMap;

use bytes::Bytes;

use crate::edge_registry::Label;
use crate::edge_registry::edge_registry_gen::common_types::b64::{self};
use crate::edge_registry::edge_registry_gen::edge_registry::client as client_gen;

pub mod schema;
pub mod thing_description;
pub mod thing_model;
pub mod xregistry;

pub use schema::*;
pub use thing_description::*;
pub use thing_model::*;
pub use xregistry::*;

// ~~~~~~~~~~~~~~~~~~~Conversion helpers~~~~~~~~~~~~~~~~~~~~~~~~~~

/// Converts a generated list of `Label` key/value pairs into a vector of [`Label`].
pub(crate) fn labels_from_gen(labels: Vec<client_gen::Label>) -> Vec<Label> {
    labels.into_iter().map(Into::into).collect()
}

/// Converts a list of [`Label`] into the generated list of `Label` key/value pairs.
pub(crate) fn labels_to_gen(labels: Vec<Label>) -> Vec<client_gen::Label> {
    labels.into_iter().map(Into::into).collect()
}

/// Converts a generated map of base64 extension values into byte buffers.
pub(crate) fn extensions_from_gen(
    extensions: HashMap<String, b64::Bytes>,
) -> HashMap<String, Bytes> {
    extensions
        .into_iter()
        .map(|(k, v)| (k, Bytes::from(v.0)))
        .collect()
}

/// Converts a map of extension byte buffers into the generated base64 type.
pub(crate) fn extensions_to_gen(extensions: HashMap<String, Bytes>) -> HashMap<String, b64::Bytes> {
    extensions
        .into_iter()
        .map(|(k, v)| (k, b64::Bytes(v.to_vec())))
        .collect()
}
