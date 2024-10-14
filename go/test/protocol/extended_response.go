// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import "github.com/Azure/iot-operations-sdks/go/protocol"

type ExtendedResponse struct {
	Response *protocol.CommandResponse[string]
	Error    error
}
