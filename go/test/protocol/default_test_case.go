package protocol

type DefaultTestCase struct {
	Prologue DefaultPrologue `toml:"prologue"`
	Actions  DefaultAction   `toml:"actions"`
}
