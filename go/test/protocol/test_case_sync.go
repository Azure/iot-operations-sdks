package protocol

type TestCaseSync struct {
	SignalEvent *string `yaml:"signal-event"`
	WaitEvent   *string `yaml:"wait-event"`
}
