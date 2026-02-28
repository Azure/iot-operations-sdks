// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::sync::LazyLock;

pub static LABELED_SYSTEMS_OF_UNITS: LazyLock<Vec<(&'static str, &'static str)>> =
    LazyLock::new(|| {
        vec![
            ("ASU", "Astronomic System Of Units"),
            ("CGS", "CGS System of Units"),
            ("CGS-EMU", "CGS System of Units - EMU"),
            ("CGS-ESU", "CGS System of Units ESU"),
            ("CGS-GAUSS", "CGS System of Units - Gaussian"),
            ("IMPERIAL", "Imperial System of Units"),
            ("PLANCK", "Planck System of Units"),
            ("SI", "International System of Units"),
            ("UNSTATED", "Unstated System Of Units"),
            ("USCS", "US Customary Unit System"),
        ]
    });
