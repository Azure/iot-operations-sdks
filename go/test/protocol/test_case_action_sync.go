package protocol

type TestCaseActionSync struct {
	SignalEvent *string `yaml:"signal-event"`
	WaitEvent   *string `yaml:"wait-event"`
}
