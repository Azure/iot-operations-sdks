package protocol

type DefaultPrologue struct {
	Executor DefaultExecutor `toml:"executor"`
	Invoker  DefaultInvoker  `toml:"invoker"`
}
