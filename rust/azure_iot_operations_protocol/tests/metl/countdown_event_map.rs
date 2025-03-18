// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use tokio::sync::Notify;
use tokio::time::timeout;

#[derive(Clone)]
pub struct CountdownEventMap {
    events: HashMap<String, Arc<(Mutex<i32>, Notify)>>,
}

#[allow(unused)]
impl CountdownEventMap {
    pub fn new() -> Self {
        Self {
            events: HashMap::new(),
        }
    }

    pub fn insert(&mut self, key: String, init_count: i32) {
        self.events
            .insert(key, Arc::new((Mutex::new(init_count), Notify::new())));
    }

    pub async fn wait(&self, key: &str) {
        let (_, notify) = &*self.events.get(key).unwrap().clone();

        notify.notified().await;

        // notify next waiter so that all waiters awaken via domino effect
        notify.notify_one();
    }

    pub async fn wait_timeout(&self, key: &str, deadline: Duration) -> Result<(), ()> {
        let (_, notify) = &*self.events.get(key).unwrap().clone();

        if timeout(deadline, notify.notified()).await.is_err() {
            return Err(());
        }

        // notify next waiter so that all waiters awaken via domino effect
        notify.notify_one();

        Ok(())
    }

    pub fn signal(&self, key: &str) {
        let (mutex, notify) = &*self.events.get(key).unwrap().clone();

        let mut remaining_count = mutex.lock().unwrap();
        *remaining_count -= 1;
        if *remaining_count < 1 {
            notify.notify_one();
        }
    }
}
