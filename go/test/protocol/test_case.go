package protocol

type TestCase struct {
	TestName    string              `yaml:"test-name"`
	Aka         []string            `yaml:"aka"`
	Description TestCaseDescription `yaml:"description"`
	Requires    []TestFeatureKind   `yaml:"requires"`
	Prologue    TestCasePrologue    `yaml:"prologue"`
	Actions     []TestCaseAction    `yaml:"actions"`
	Epilogue    TestCaseEpilogue    `yaml:"epilogue"`
}
