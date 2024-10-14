// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package retrypolicy

import "time"

const (
	defaultMaxInterval = 30 * time.Second
	defaultWithJitter  = true
)
