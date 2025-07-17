// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Envoys for Property state operations.

/// This module contains the property maintainer implementation.
pub mod maintainer;

/// This module contains the property consumer implementation.
pub mod consumer;

/// Re-export the property maintainer and consumer for ease of use.
pub use consumer::Consumer;
pub use maintainer::Maintainer;
