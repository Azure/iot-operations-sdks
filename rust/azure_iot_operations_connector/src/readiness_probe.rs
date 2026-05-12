// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Readiness probe support for connectors running in Kubernetes.

use std::path::PathBuf;
use std::{fs, process};

/// Default path for the readiness probe file.
pub const DEFAULT_READINESS_FILE: &str = "/tmp/connector-ready";

/// Default CLI argument that triggers the readiness probe check.
pub const DEFAULT_READINESS_ARG: &str = "readiness-probe";

/// Abstraction for signaling connector readiness to Kubernetes.
///
/// Methods are synchronous and infallible by design. Implementations that need
/// async or fallible work (e.g., HTTP or TCP probes) should perform that work in a background task
/// and use the trait methods only to toggle in-memory state (e.g., via channels or atomics).
///
/// Methods return `()` rather than `Result`. There is no meaningful action a caller can take
/// when a state transition fails (e.g., a file write error): crashing the connector on a probe
/// write failure would be strictly worse than letting it keep running. Implementations should
/// therefore log errors instead of propagating them. If an implementation needs to expose
/// errors for observability or external reporting, it can surface them through a channel held
/// by the implementor rather than through the trait API.
pub trait ReadinessProbe: Send + Sync {
    /// Signal that the connector is ready.
    fn set_ready(&self);
    /// Signal that the connector is not ready.
    fn set_not_ready(&self);
}

/// File-based readiness probe using a Kubernetes exec probe pattern.
///
/// Creates or removes a marker file to signal readiness. The connector binary
/// itself handles the probe check via [`ExecReadinessProbe::handle_probe_if_requested`].
///
/// The marker file lives at [`DEFAULT_READINESS_FILE`] by default. The directory holding
/// it must be writable by the connector process.
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

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    /// Creates a probe whose marker file lives inside a fresh temp directory.
    /// Returns the probe and the [`TempDir`] guard (which deletes the directory on drop).
    fn probe_with_temp_file() -> (ExecReadinessProbe, TempDir) {
        let dir = TempDir::with_prefix("readiness-probe-test").unwrap();
        let path = dir.path().join("connector-ready");
        let probe = ExecReadinessProbe::new(path, DEFAULT_READINESS_ARG);
        (probe, dir)
    }

    #[test]
    fn set_ready_creates_marker_file() {
        let (probe, dir) = probe_with_temp_file();
        let marker = dir.path().join("connector-ready");

        assert!(!marker.exists());
        probe.set_ready();
        assert!(marker.exists());
    }

    #[test]
    fn set_not_ready_removes_marker_file() {
        let (probe, dir) = probe_with_temp_file();
        let marker = dir.path().join("connector-ready");

        probe.set_ready();
        assert!(marker.exists());
        probe.set_not_ready();
        assert!(!marker.exists());
    }

    #[test]
    fn set_not_ready_is_noop_when_file_missing() {
        let (probe, dir) = probe_with_temp_file();
        let marker = dir.path().join("connector-ready");

        assert!(!marker.exists());
        probe.set_not_ready();
        assert!(!marker.exists());
    }

    #[test]
    fn set_ready_is_idempotent() {
        let (probe, dir) = probe_with_temp_file();
        let marker = dir.path().join("connector-ready");

        probe.set_ready();
        probe.set_ready();
        assert!(marker.exists());
    }

    #[test]
    fn round_trip_leaves_no_marker_file() {
        let (probe, dir) = probe_with_temp_file();
        let marker = dir.path().join("connector-ready");

        probe.set_ready();
        probe.set_not_ready();
        assert!(!marker.exists());
    }
}
