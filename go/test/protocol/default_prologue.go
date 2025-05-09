// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultPrologue struct {
	Executor DefaultExecutor `toml:"executor"`
	Invoker  DefaultInvoker  `toml:"invoker"`
	Receiver DefaultReceiver `toml:"receiver"`
	Sender   DefaultSender   `toml:"sender"`
}
