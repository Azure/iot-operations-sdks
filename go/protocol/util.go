// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

// Must ensures an object is created, or panics on error. Used to create global
// instances, e.g. of an Application state.
func Must[T any](t T, e error) T {
	if e != nil {
		panic(e)
	}
	return t
}
