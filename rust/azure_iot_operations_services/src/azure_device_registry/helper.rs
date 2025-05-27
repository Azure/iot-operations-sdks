// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Common helpers

/// Converts an `Option<Vec<T>>` into an `Option<Vec<U>>` where `T` can be converted into `U`.
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
