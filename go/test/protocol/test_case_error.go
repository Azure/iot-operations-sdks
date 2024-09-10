package protocol

type TestCaseError struct {
	Kind          TestErrorKind `yaml:"kind"`
	Message       *string       `yaml:"message"`
	PropertyName  *string       `yaml:"property-name"`
	PropertyValue *string       `yaml:"property-value"`
}
