// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;
use tokio::time::Duration;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseDuration {
    #[serde(default)]
    pub hours: i32,

    #[serde(default)]
    pub minutes: i32,

    #[serde(default)]
    pub seconds: i32,

    #[serde(default)]
    pub milliseconds: i32,

    #[serde(default)]
    pub microseconds: i32,
}

impl TestCaseDuration {
    pub fn to_duration(&self) -> Duration {
        let hours = self.hours;
        let minutes = 60 * hours + self.minutes;
        let seconds = 60 * minutes + self.seconds;
        let millis = 1000 * seconds + self.milliseconds;
        let micros = 1000 * millis + self.microseconds;
        Duration::from_micros(micros.try_into().unwrap())
    }
}
