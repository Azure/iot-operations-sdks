package protocol

type DefaultInvoker struct {
	CommandName         *string `toml:"command-name"`
	RequestTopic        *string `toml:"request-topic"`
	ModelID             *string `toml:"model-id"`
	ResponseTopicPrefix *string `toml:"response-topic-prefix"`
	ResponseTopicSuffix *string `toml:"response-topic-suffix"`
}

func (invoker DefaultInvoker) GetCommandName() *string {
	if invoker.CommandName == nil {
		return nil
	}

	commandName := *invoker.CommandName
	return &commandName
}

func (invoker DefaultInvoker) GetRequestTopic() *string {
	if invoker.RequestTopic == nil {
		return nil
	}

	requestTopic := *invoker.RequestTopic
	return &requestTopic
}

func (invoker DefaultInvoker) GetModelID() *string {
	if invoker.ModelID == nil {
		return nil
	}

	modelID := *invoker.ModelID
	return &modelID
}

func (invoker DefaultInvoker) GetResponseTopicPrefix() *string {
	if invoker.ResponseTopicPrefix == nil {
		return nil
	}

	responseTopicPrefix := *invoker.ResponseTopicPrefix
	return &responseTopicPrefix
}

func (invoker DefaultInvoker) GetResponseTopicSuffix() *string {
	if invoker.ResponseTopicSuffix == nil {
		return nil
	}

	responseTopicSuffix := *invoker.ResponseTopicSuffix
	return &responseTopicSuffix
}
