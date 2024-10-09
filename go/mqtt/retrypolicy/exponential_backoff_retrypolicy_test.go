package retrypolicy_test

import (
	"context"
	"errors"
	"log/slog"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/mqtt/retrypolicy"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"
)

type Mock struct {
	mock.Mock
}

var errRetryable = errors.New("this error is retryable")

// Mocked retry executed function.
func (m *Mock) TaskExec(context.Context) error {
	args := m.Called()
	return args.Error(0)
}

// Mocked retry condition function.
func (*Mock) TaskCond(err error) bool {
	return err == errRetryable
}

func TestNoRetry(t *testing.T) {
	m := new(Mock)
	m.On("TaskExec").Return(nil)

	ctx := context.Background()
	logger := slog.Default()

	retry := retrypolicy.NewExponentialBackoffRetryPolicy()
	err := retry.Start(ctx, logger.Error, retrypolicy.Task{
		Name: "TestNoRetry",
		Exec: m.TaskExec,
		Cond: m.TaskCond,
	})

	require.NoError(t, err)
	m.AssertNumberOfCalls(t, "TaskExec", 1)
}

func TestMaxRetries(t *testing.T) {
	m := new(Mock)
	m.On("TaskExec").Return(errRetryable)

	ctx := context.Background()
	logger := slog.Default()

	retry := retrypolicy.NewExponentialBackoffRetryPolicy(
		retrypolicy.WithMaxRetries(3),
	)
	err := retry.Start(ctx, logger.Error, retrypolicy.Task{
		Name: "TestMaxRetries",
		Exec: m.TaskExec,
		Cond: m.TaskCond,
	})

	require.EqualError(t, err, errRetryable.Error())
	m.AssertNumberOfCalls(t, "TaskExec", 3)
}

func TestRetryUntilSuccess(t *testing.T) {
	m := new(Mock)
	m.On("TaskExec").Twice().Return(errRetryable)
	m.On("TaskExec").Once().Return(nil)

	ctx := context.Background()
	logger := slog.Default()

	retry := retrypolicy.NewExponentialBackoffRetryPolicy()
	err := retry.Start(ctx, logger.Error, retrypolicy.Task{
		Name: "TestRetryUntilSuccess",
		Exec: m.TaskExec,
		Cond: m.TaskCond,
	})

	require.NoError(t, err)
	m.AssertNumberOfCalls(t, "TaskExec", 3)
}
