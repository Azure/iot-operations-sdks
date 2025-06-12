// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Contains common modules shared between the clients for the various services of Azure IoT Operations

// NOTE: submodules should be behind the feature flags of the clients that use them to ensure they
// are only compiled when necessary.

#[cfg(feature = "state_store")]
pub mod dispatcher {
    //! Provides a convenience for dispatching to a receiver based on an ID.

    use std::{collections::HashMap, sync::Mutex};

    use thiserror::Error;
    use tokio::sync::mpsc::{
        UnboundedReceiver, UnboundedSender, error::SendError, unbounded_channel,
    };

    pub type Receiver<T> = UnboundedReceiver<T>;

    /// Error when registering a new receiver
    #[derive(Error, Debug)]
    pub enum RegisterError {
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

    #[derive(Debug, Eq, PartialEq, Clone)]
    pub enum DispatchErrorKind {
        SendError,
        NotFound(String), // receiver ID
    }

    impl std::fmt::Display for DispatchErrorKind {
        fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
            match self {
                DispatchErrorKind::SendError => write!(f, "Failed to send message"),
                DispatchErrorKind::NotFound(id) => write!(f, "Receiver with ID '{id}' not found"),
            }
        }
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
    pub struct Dispatcher<T> {
        tx_map: Mutex<HashMap<String, UnboundedSender<T>>>,
    }

    impl<T> Dispatcher<T> {
        /// Returns a new instance of Dispatcher
        pub fn new() -> Self {
            Self {
                tx_map: Mutex::new(HashMap::new()),
            }
        }

        /// Registers a new receiver with the given ID, returning the new receiver.
        ///
        /// Returns an error if a receiver with the same ID is already registered
        pub fn register_receiver(&self, receiver_id: String) -> Result<Receiver<T>, RegisterError> {
            let mut tx_map = self.tx_map.lock().unwrap();
            if tx_map.get(&receiver_id).is_some() {
                return Err(RegisterError::AlreadyRegistered(receiver_id));
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
        pub fn unregister_receiver(&self, receiver_id: &str) -> bool {
            self.tx_map.lock().unwrap().remove(receiver_id).is_some()
        }

        /// Unregisters all receivers, returning the number of receivers that were unregistered.
        /// This closes all associated channels.
        pub fn unregister_all(&self) -> usize {
            self.tx_map.lock().unwrap().drain().count()
        }

        /// Dispatches a message to the receiver associated with the provided ID.
        pub fn dispatch(&self, receiver_id: &str, message: T) -> Result<(), DispatchError<T>> {
            if let Some(tx) = self.tx_map.lock().unwrap().get(receiver_id) {
                Ok(tx.send(message)?)
            } else {
                Err(DispatchError {
                    data: message,
                    kind: DispatchErrorKind::NotFound(receiver_id.to_string()),
                })
            }
        }
    }
}
