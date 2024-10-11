package protocol

import (
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"gopkg.in/yaml.v3"
)

const (
	HeaderNameKey    = "header-name"
	HeaderValueKey   = "header-value"
	TimeoutNameKey   = "timeout-name"
	TimeoutValueKey  = "timeout-value"
	PropertyNameKey  = "property-name"
	PropertyValueKey = "property-value"
	CommandNameKey   = "command-name"
)

type testCaseCatch struct {
	ErrorKind     string             `yaml:"error-kind"`
	InApplication bool               `yaml:"in-application"`
	IsShallow     bool               `yaml:"is-shallow"`
	IsRemote      bool               `yaml:"is-remote"`
	StatusCode    any                `yaml:"status-code"`
	Message       *string            `yaml:"message"`
	Supplemental  map[string]*string `yaml:"supplemental"`
}

type TestCaseCatch struct {
	testCaseCatch
}

func (catch *TestCaseCatch) UnmarshalYAML(node *yaml.Node) error {
	*catch = TestCaseCatch{}

	catch.StatusCode = false

	return node.Decode(&catch.testCaseCatch)
}

func (catch *TestCaseCatch) GetErrorKind() errors.Kind {
	switch catch.ErrorKind {
	case "missing header":
		return errors.HeaderMissing
	case "invalid header":
		return errors.HeaderInvalid
	case "invalid payload":
		return errors.PayloadInvalid
	case "timeout":
		return errors.Timeout
	case "cancellation":
		return errors.Cancellation
	case "invalid configuration":
		return errors.ConfigurationInvalid
	case "invalid argument":
		return errors.ArgumentInvalid
	case "invalid state":
		return errors.StateInvalid
	case "internal logic error":
		return errors.InternalLogicError
	case "unknown error":
		return errors.UnknownError
	case "invocation error":
		return errors.InvocationException
	case "execution error":
		return errors.ExecutionException
	case "mqtt error":
		return errors.MqttError
	case "request version not supported":
		return errors.UnsupportedRequestVersion
	case "response version not supported":
		return errors.UnsupportedResponseVersion
	default:
		return errors.Kind(-1)
	}
}
