// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{collections::HashMap, fmt::Debug, hash::Hash};

use parking_lot::Mutex;
use thiserror::Error;
use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender, error::SendError, unbounded_channel};

/// Unbounded channel receiver for receiving dispatched messages based on the registered ID.
pub type Receiver<T> = UnboundedReceiver<T>;

/// Error when registering a new receiver
#[derive(Error, Debug)]
pub enum RegisterError {
    /// A receiver with the same ID is already registered
    #[error("receiver with id {0} already registered")]
    AlreadyRegistered(String),
}

/// Error when dispatching a message to a receiver
#[derive(PartialEq, Eq, Clone, Error, Debug)]
#[error("{kind}")]
pub struct DispatchError<T> {
    /// The message that could not be sent
    pub data: T,
    /// The kind of error that occurred
    pub kind: DispatchErrorKind,
}

/// Error kind when dispatching a message to a receiver
#[derive(Debug, Eq, PartialEq, Clone, Error)]
pub enum DispatchErrorKind {
    /// The message could not be sent to the receiver
    #[error("Failed to send message")]
    SendError,
    /// There was no receiver with the given ID registered
    #[error("Receiver with ID '{0}' not found")]
    NotFound(String), // receiver ID
}

impl<T> From<SendError<T>> for DispatchError<T> {
    fn from(err: SendError<T>) -> Self {
        Self {
            data: err.0,
            kind: DispatchErrorKind::SendError,
        }
    }
}
/// Dispatches messages to receivers based on ID
#[derive(Default)]
pub struct Dispatcher<T, H>
where
    H: Eq + Hash + Debug + Clone,
{
    tx_map: Mutex<HashMap<H, UnboundedSender<T>>>,
}

impl<T, H> Dispatcher<T, H>
where
    H: Eq + Hash + Debug + Clone,
{
    /// Returns a new instance of Dispatcher
    #[must_use]
    pub fn new() -> Self {
        Self {
            tx_map: Mutex::new(HashMap::new()),
        }
    }

    /// Registers a new receiver with the given ID, returning the new receiver.
    ///
    /// # Errors
    /// Returns an error if a receiver with the same ID is already registered
    pub fn register_receiver(&self, receiver_id: H) -> Result<Receiver<T>, RegisterError> {
        let mut tx_map = self.tx_map.lock();
        if tx_map.get(&receiver_id).is_some() {
            return Err(RegisterError::AlreadyRegistered(format!("{receiver_id:?}")));
        }
        let (tx, rx) = unbounded_channel();
        tx_map.insert(receiver_id, tx);
        Ok(rx)
    }

    /// Unregisters a receiver with the given ID, if it exists.
    /// This closes the associated channel.
    ///
    /// Returns true if a receiver was unregistered, returns false if the provided ID
    /// was not associated with a registered receiver.
    pub fn unregister_receiver(&self, receiver_id: &H) -> bool {
        self.tx_map.lock().remove(receiver_id).is_some()
    }

    /// Unregisters all receivers, returning the number of receivers that were unregistered.
    /// This closes all associated channels.
    pub fn unregister_all(&self) -> usize {
        self.tx_map.lock().drain().count()
    }

    /// Dispatches a message to the receiver associated with the provided ID.
    ///
    /// # Errors
    /// Returns an error if no receiver is registered with the provided ID,
    /// or if the message could not be sent to the receiver.
    pub fn dispatch(&self, receiver_id: &H, message: T) -> Result<(), DispatchError<T>> {
        if let Some(tx) = self.tx_map.lock().get(receiver_id) {
            Ok(tx.send(message)?)
        } else {
            Err(DispatchError {
                data: message,
                kind: DispatchErrorKind::NotFound(format!("{receiver_id:?}")),
            })
        }
    }

    /// Returns all currently tracked receiver ids
    #[allow(dead_code)]
    pub fn get_all_receiver_ids(&self) -> Vec<H> {
        let tx_map = self.tx_map.lock();
        tx_map.keys().cloned().collect()
    }
}

#[cfg(test)]
mod tests {
    use test_case::{test_case, test_matrix};

    use super::*;

    fn create_dispatcher() -> Dispatcher<String, String> {
        Dispatcher::new()
    }

    #[test]
    fn test_new_dispatcher_has_no_receivers() {
        let dispatcher = create_dispatcher();
        assert!(dispatcher.get_all_receiver_ids().is_empty());
    }

    #[test_case("id1"; "simple id")]
    #[test_case(""; "empty string id")]
    #[test_case("id/with/slashes"; "id with slashes")]
    fn test_register_receiver_success(id: &str) {
        let dispatcher = create_dispatcher();
        let _rx = dispatcher.register_receiver(id.to_string()).unwrap();
        assert_eq!(dispatcher.get_all_receiver_ids(), vec![id.to_string()]);
    }

    #[test]
    fn test_register_receiver_duplicate_returns_error() {
        let dispatcher = create_dispatcher();
        let _rx = dispatcher.register_receiver("dup".to_string()).unwrap();
        let result = dispatcher.register_receiver("dup".to_string());
        assert!(matches!(
            result.unwrap_err(),
            RegisterError::AlreadyRegistered(_)
        ));
    }

    #[test]
    fn test_register_multiple_distinct_receivers() {
        let dispatcher = create_dispatcher();
        let _rx1 = dispatcher.register_receiver("a".to_string()).unwrap();
        let _rx2 = dispatcher.register_receiver("b".to_string()).unwrap();
        let _rx3 = dispatcher.register_receiver("c".to_string()).unwrap();
        let mut ids = dispatcher.get_all_receiver_ids();
        ids.sort();
        assert_eq!(ids, vec!["a", "b", "c"]);
    }

    #[test_matrix([true, false], [true, false])]
    fn test_unregister_receiver(register_first: bool, unregister: bool) {
        let dispatcher = create_dispatcher();
        if register_first {
            let _rx = dispatcher.register_receiver("id".to_string()).unwrap();
        }
        if unregister {
            let result = dispatcher.unregister_receiver(&"id".to_string());
            assert_eq!(result, register_first);
        }
        let expected_count = usize::from(register_first && !unregister);
        assert_eq!(dispatcher.get_all_receiver_ids().len(), expected_count);
    }

    #[test_case(0, 0; "unregister all with none registered")]
    #[test_case(1, 1; "unregister all with one registered")]
    #[test_case(3, 3; "unregister all with three registered")]
    fn test_unregister_all(register_count: usize, expected_unregistered: usize) {
        let dispatcher = create_dispatcher();
        for i in 0..register_count {
            let _rx = dispatcher.register_receiver(format!("id{i}")).unwrap();
        }
        assert_eq!(dispatcher.unregister_all(), expected_unregistered);
        assert!(dispatcher.get_all_receiver_ids().is_empty());
    }

    #[test]
    fn test_dispatch_success() {
        let dispatcher = create_dispatcher();
        let mut rx = dispatcher.register_receiver("id".to_string()).unwrap();
        dispatcher
            .dispatch(&"id".to_string(), "hello".to_string())
            .unwrap();
        let msg = rx.try_recv().unwrap();
        assert_eq!(msg, "hello");
    }

    #[test]
    fn test_dispatch_multiple_messages_in_order() {
        let dispatcher = create_dispatcher();
        let mut rx = dispatcher.register_receiver("id".to_string()).unwrap();
        for i in 0..5 {
            dispatcher
                .dispatch(&"id".to_string(), format!("msg{i}"))
                .unwrap();
        }
        for i in 0..5 {
            assert_eq!(rx.try_recv().unwrap(), format!("msg{i}"));
        }
    }

    #[test]
    fn test_dispatch_to_nonexistent_receiver_returns_not_found() {
        let dispatcher = create_dispatcher();
        let result = dispatcher.dispatch(&"missing".to_string(), "data".to_string());
        let err = result.unwrap_err();
        assert_eq!(err.data, "data");
        assert!(matches!(err.kind, DispatchErrorKind::NotFound(_)));
    }

    #[test]
    fn test_dispatch_to_dropped_receiver_returns_send_error() {
        let dispatcher = create_dispatcher();
        let rx = dispatcher.register_receiver("id".to_string()).unwrap();
        drop(rx);
        let result = dispatcher.dispatch(&"id".to_string(), "data".to_string());
        let err = result.unwrap_err();
        assert_eq!(err.data, "data");
        assert!(matches!(err.kind, DispatchErrorKind::SendError));
    }

    #[test]
    fn test_dispatch_to_correct_receiver_among_multiple() {
        let dispatcher = create_dispatcher();
        let mut rx1 = dispatcher.register_receiver("r1".to_string()).unwrap();
        let mut rx2 = dispatcher.register_receiver("r2".to_string()).unwrap();
        dispatcher
            .dispatch(&"r2".to_string(), "for_r2".to_string())
            .unwrap();
        assert!(rx1.try_recv().is_err());
        assert_eq!(rx2.try_recv().unwrap(), "for_r2");
    }

    #[test]
    fn test_register_after_unregister_same_id() {
        let dispatcher = create_dispatcher();
        let _rx1 = dispatcher.register_receiver("id".to_string()).unwrap();
        dispatcher.unregister_receiver(&"id".to_string());
        let mut rx2 = dispatcher.register_receiver("id".to_string()).unwrap();
        dispatcher
            .dispatch(&"id".to_string(), "new".to_string())
            .unwrap();
        assert_eq!(rx2.try_recv().unwrap(), "new");
    }

    #[test]
    fn test_unregister_closes_channel() {
        let dispatcher = create_dispatcher();
        let mut rx = dispatcher.register_receiver("id".to_string()).unwrap();
        dispatcher.unregister_receiver(&"id".to_string());
        assert!(rx.try_recv().is_err());
    }

    #[test]
    fn test_unregister_all_closes_all_channels() {
        let dispatcher = create_dispatcher();
        let mut rx1 = dispatcher.register_receiver("a".to_string()).unwrap();
        let mut rx2 = dispatcher.register_receiver("b".to_string()).unwrap();
        dispatcher.unregister_all();
        assert!(rx1.try_recv().is_err());
        assert!(rx2.try_recv().is_err());
    }

    #[test]
    fn test_dispatcher_with_integer_keys() {
        let dispatcher: Dispatcher<&str, u32> = Dispatcher::new();
        let mut rx = dispatcher.register_receiver(42).unwrap();
        dispatcher.dispatch(&42, "answer").unwrap();
        assert_eq!(rx.try_recv().unwrap(), "answer");
    }
}
