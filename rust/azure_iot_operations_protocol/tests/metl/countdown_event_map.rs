// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::sync::{Arc, Condvar, Mutex};
use std::time::Duration;

// This code is loosely based on these examples of Condvar usage:
// - https://doc.rust-lang.org/std/sync/struct.Condvar.html#examples-3
// - https://doc.rust-lang.org/std/sync/struct.Condvar.html#examples-6

#[derive(Clone)]
pub struct CountdownEventMap {
    events: HashMap<String, Arc<(Mutex<i32>, Condvar)>>,
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
            .insert(key, Arc::new((Mutex::new(init_count), Condvar::new())));
    }

    pub fn wait(&self, key: &str) {
        let (mutex, cvar) = &*self.events.get(key).unwrap().clone();
        let _guard = cvar
            .wait_while(mutex.lock().unwrap(), |remaining_count| {
                *remaining_count > 0
            })
            .unwrap();
    }

    pub fn wait_timeout(&self, key: &str, timeout: Duration) -> Result<(), ()> {
        let (mutex, cvar) = &*self.events.get(key).unwrap().clone();
        let result = cvar
            .wait_timeout_while(mutex.lock().unwrap(), timeout, |remaining_count| {
                *remaining_count > 0
            })
            .unwrap();
        if result.1.timed_out() {
            Err(())
        } else {
            Ok(())
        }
    }

    pub fn signal(&self, key: &str) {
        let (mutex, cvar) = &*self.events.get(key).unwrap().clone();
        let mut remaining_count = mutex.lock().unwrap();
        *remaining_count -= 1;
        if *remaining_count < 1 {
            cvar.notify_all();
        }
    }
}
