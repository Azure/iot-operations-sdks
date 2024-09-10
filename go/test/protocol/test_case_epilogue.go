package protocol

type TestCaseEpilogue struct {
	SubscribedTopics     []string                   `yaml:"subscribed-topics"`
	PublicationCount     *int                       `yaml:"publication-count"`
	PublishedMessages    []TestCasePublishedMessage `yaml:"published-messages"`
	AcknowledgementCount *int                       `yaml:"acknowledgement-count"`
	ExecutionCount       *int                       `yaml:"execution-count"`
	ExecutionCounts      map[int]int                `yaml:"execution-counts"`
	Catch                *TestCaseCatch             `yaml:"catch"`
}
