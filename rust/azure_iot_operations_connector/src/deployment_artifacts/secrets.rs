// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Secrets interface for deployment artifacts
use std::path::PathBuf;

use tokio::sync::watch;

// TODO: struct?
pub enum ConnectorSecretError {
    SecretNotFound,
    SecretReadError,
    SecretWatchError,
}

pub struct ConnectorSecrets {
    metadata_pathbuf: PathBuf,
    secret_pathbuf: PathBuf,
}

impl ConnectorSecrets {

    // TODO: Result<Option<Secret>>?
    pub fn get_secret(&self, secret_name: &str) -> Result<Secret, ConnectorSecretError> {
        // read file with secret name from metadata pathbuf
        let alias_pathbuf = self.metadata_pathbuf.join(secret_name);
        if !alias_pathbuf.exists() {
            return Err(ConnectorSecretError::SecretNotFound);
        }
        let alias = std::fs::read_to_string(&alias_pathbuf).map_err(|_| ConnectorSecretError::SecretReadError)?;

        // Read secret value
        let secret_pathbuf = self.secret_pathbuf.join(alias.trim());
        if !secret_pathbuf.exists() {
            return Err(ConnectorSecretError::SecretNotFound);
        }
        let secret_data = std::fs::read_to_string(&secret_pathbuf).map_err(|_| ConnectorSecretError::SecretReadError)?;

        unimplemented!()

        // todo: errors on fileread here?
    }
}


#[derive(Clone, Debug)]
pub struct Secret {
    update_rx: watch::Receiver<String>, //TODO: type?
}

impl Secret {
    pub fn value(&self) -> &str {
        //self.update_rx.borrow_and_update()

        unimplemented!()
    }

    pub async fn changed(&self) {
        // TODO: need some kind of enum to indicate what happened?
        // e.g. Updated vs Deleted
        unimplemented!()
    }
}