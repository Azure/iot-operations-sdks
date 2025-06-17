// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Utilities for extracting data from filemounts in an Akri deployment

use std::env::{self, VarError};
use std::ffi::OsString;
use std::path::PathBuf;

use thiserror::Error;

pub mod azure_device_registry;
pub mod connector;
pub mod observability;
#[cfg(test)]
mod test_utils;

// TODO: Add common artifact structs and helpers here once implementation is unified

/// Indicates an error occurred while parsing the artifacts in an Akri deployment
#[derive(Error, Debug)]
#[error(transparent)]
pub struct DeploymentArtifactError(#[from] DeploymentArtifactErrorRepr);

/// Represents the type of error encountered while parsing artifacts in an Akri deployment
#[derive(Error, Debug)]
enum DeploymentArtifactErrorRepr {
    /// A required environment variable was not found
    #[error("Required environment variable missing: {0}")]
    EnvVarMissing(String),
    /// The value contained in an environment variable was malformed
    #[error("Environment variable value malformed: {0}")]
    EnvVarValueMalformed(String),
    /// A specified mount path could not be found in the filesystem
    #[error("Specified mount path not found in filesystem: {0:?}")]
    MountPathMissing(OsString),
    /// A required file path could not be found in the filesystem
    #[error("Required file path not found: {0:?}")]
    FilePathMissing(OsString),
    /// An error occurred while trying to read a file in the filesystem
    #[error(transparent)]
    FileReadError(#[from] std::io::Error),
    /// JSON data could not be parsed
    #[error(transparent)]
    JsonParseError(#[from] serde_json::Error),
}

/// Helper function to get an environment variable as a string.
fn string_from_environment(key: &str) -> Result<Option<String>, DeploymentArtifactErrorRepr> {
    match env::var(key) {
        Ok(value) => Ok(Some(value)),
        Err(VarError::NotPresent) => Ok(None),
        Err(VarError::NotUnicode(_)) => Err(DeploymentArtifactErrorRepr::EnvVarValueMalformed(
            key.to_string(),
        )),
    }
}

/// Helper function to validate a mount path and return it as a `PathBuf`.
fn valid_mount_pathbuf_from(mount_path_s: String) -> Result<PathBuf, DeploymentArtifactErrorRepr> {
    let mount_path = PathBuf::from(mount_path_s);
    if !mount_path.exists() {
        return Err(DeploymentArtifactErrorRepr::MountPathMissing(
            mount_path.into(),
        ));
    }
    Ok(mount_path)
}
