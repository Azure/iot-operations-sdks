// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Common helpers
use std::collections::HashMap;
use std::hash::Hash;

/// Converts an into an `Option<Vec<U>>` where `T` can be converted into `U`.
pub trait ConvertOptionVec<T, U>
where
    T: Into<U>,
{
    fn option_vec_into(self) -> Option<Vec<U>>;
}

impl<T, U> ConvertOptionVec<T, U> for Option<Vec<T>>
where
    T: Into<U>,
{
    fn option_vec_into(self) -> Option<Vec<U>> {
        self.map(|vec| vec.into_iter().map(Into::into).collect())
    }
}

impl<T, U> ConvertOptionVec<T, U> for Vec<T>
where
    T: Into<U>,
{
    fn option_vec_into(self) -> Option<Vec<U>> {
        if self.is_empty() {
            None
        } else {
            Some(self.into_iter().map(Into::into).collect())
        }
    }
}

/// Converts an into an `Option<HashMap<K, U>>` where `T` can be converted into `U`.
pub trait ConvertOptionMap<T, U, K>
where
    T: Into<U>,
{
    fn option_map_into(self) -> Option<HashMap<K, U>>;
}

impl<T, U, K> ConvertOptionMap<T, U, K> for Option<HashMap<K, T>>
where
    T: Into<U>,
    K: Hash + Eq,
{
    fn option_map_into(self) -> Option<HashMap<K, U>> {
        self.map(|map| map.into_iter().map(|(k, v)| (k, v.into())).collect())
    }
}

impl<T, U, K> ConvertOptionMap<T, U, K> for HashMap<K, T>
where
    T: Into<U>,
    K: Hash + Eq,
{
    fn option_map_into(self) -> Option<HashMap<K, U>> {
        if self.is_empty() {
            None
        } else {
            Some(self.into_iter().map(|(k, v)| (k, v.into())).collect())
        }
    }
}
