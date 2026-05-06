// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Readiness probe support for connectors running in Kubernetes.

use std::fmt::Debug;
use std::path::PathBuf;
use std::{fs, process};

/// Default path for the readiness probe file.
pub const DEFAULT_READINESS_FILE: &str = "/tmp/connector-ready";

/// Default CLI argument that triggers the readiness probe check.
pub const DEFAULT_READINESS_ARG: &str = "readiness-probe";

/// Abstraction for signaling connector readiness to Kubernetes.
pub trait ReadinessProbe: Send + Sync + Debug {
    /// Signal that the connector is ready.
    fn set_ready(&self);
    /// Signal that the connector is not ready.
    fn set_not_ready(&self);
}

/// File-based readiness probe using a Kubernetes exec probe pattern.
///
/// Creates or removes a marker file to signal readiness. The connector binary
/// itself handles the probe check via [`ExecReadinessProbe::handle_probe_if_requested`].
#[derive(Debug)]
pub struct ExecReadinessProbe {
    readiness_file: PathBuf,
    readiness_arg: String,
}

impl ExecReadinessProbe {
    /// Creates a new [`ExecReadinessProbe`] with the given file path and CLI argument.
    pub fn new(readiness_file: impl Into<PathBuf>, readiness_arg: impl Into<String>) -> Self {
        Self {
            readiness_file: readiness_file.into(),
            readiness_arg: readiness_arg.into(),
        }
    }

    /// Call early in `main()` before normal initialization. If the process was
    /// invoked with the readiness-probe subcommand, checks file existence and
    /// exits with code 0 (ready) or 1 (not ready).
    pub fn handle_probe_if_requested(&self) {
        if std::env::args().any(|arg| arg == self.readiness_arg) {
            if self.readiness_file.exists() {
                process::exit(0);
            } else {
                process::exit(1);
            }
        }
    }
}

impl Default for ExecReadinessProbe {
    fn default() -> Self {
        Self::new(DEFAULT_READINESS_FILE, DEFAULT_READINESS_ARG)
    }
}

impl ReadinessProbe for ExecReadinessProbe {
    fn set_ready(&self) {
        if let Err(e) = fs::write(&self.readiness_file, "") {
            log::error!("Failed to create readiness file: {e}");
        }
    }

    fn set_not_ready(&self) {
        if let Err(e) = fs::remove_file(&self.readiness_file)
            && e.kind() != std::io::ErrorKind::NotFound
        {
            log::error!("Failed to remove readiness file: {e}");
        }
    }
}
