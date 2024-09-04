package protocol

type TestCaseDescription struct {
	Condition string `yaml:"condition"`
	Expect    string `yaml:"expect"`
}
