// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Common helpers

// TODO: these should not be necessary with correct trait implementations

/// Helper fn to convert `Option<Vec<T>>` to `Option<Vec<U>>`
pub fn option_vec_from<T, U>(source: Option<Vec<T>>, into_fn: impl Fn(T) -> U) -> Option<Vec<U>> {
    source.map(|vec| vec.into_iter().map(into_fn).collect())
}

/// Helper fn to convert `Option<Vec<T>>` to `Vec<U>`, where `Vec<U>` will be an empty Vec if source was `None`
pub fn vec_from_option_vec<T, U>(source: Option<Vec<T>>, into_fn: impl Fn(T) -> U) -> Vec<U> {
    source.map_or(vec![], |vec| vec.into_iter().map(into_fn).collect())
}
