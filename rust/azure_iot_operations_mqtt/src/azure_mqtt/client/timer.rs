// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::pin::Pin;
use std::task::{Context, Poll};

use tokio::time::{Duration, Sleep};

pub struct Timer {
    inner: Pin<Box<Sleep>>,
    duration: Duration,
}

impl Timer {
    pub fn new(duration: Duration) -> Self {
        Self {
            inner: Box::pin(tokio::time::sleep(duration)),
            duration,
        }
    }

    pub fn reset(&mut self) {
        self.inner
            .as_mut()
            .reset(tokio::time::Instant::now() + self.duration);
    }

    pub fn remaining_duration(&self) -> Duration {
        let deadline = self.deadline();
        deadline.saturating_duration_since(tokio::time::Instant::now())
    }

    pub fn deadline(&self) -> tokio::time::Instant {
        self.inner.as_ref().deadline()
    }
}

impl Future for Timer {
    type Output = ();

    fn poll(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Self::Output> {
        self.inner.as_mut().poll(cx)
    }
}
