// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

pub use crate::deployment_artifacts::connector::DeploymentArtifactError;



pub struct ConnectorSecrets {
    //metadata_mount: 
}

impl ConnectorSecrets {
    pub fn new_from() -> Self {
        unimplemented!()
    }

    pub fn get_secret(&self, secret_name: &str) -> Option<String> {
        None
    }


}


// lazily read secrets at user request?
// or keep a running understanding of updated secrets on their behalf?