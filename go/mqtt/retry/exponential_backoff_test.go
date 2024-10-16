// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package retry_test

import (
	"context"
	"errors"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/mqtt/retry"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"
)

type Mock struct {
	mock.Mock
}

var errRetryable = errors.New("this error is retryable")

// Mocked retry executed function.
func (m *Mock) Task(context.Context) (bool, error) {
	args := m.Called()
	return args.Bool(0), args.Error(1)
}

func TestNoRetry(t *testing.T) {
	m := new(Mock)
	m.On("Task").Return(false, nil)

	ctx := context.Background()

	r := retry.ExponentialBackoff{}
	err := r.Start(ctx, "TestNoRetry", m.Task)

	require.NoError(t, err)
	m.AssertNumberOfCalls(t, "Task", 1)
}

func TestMaxAttempts(t *testing.T) {
	m := new(Mock)
	m.On("Task").Return(true, errRetryable)

	ctx := context.Background()

	r := retry.ExponentialBackoff{MaxAttempts: 3}
	err := r.Start(ctx, "TestMaxAttempts", m.Task)

	require.EqualError(t, err, errRetryable.Error())
	m.AssertNumberOfCalls(t, "Task", 3)
}

func TestRetryUntilSuccess(t *testing.T) {
	m := new(Mock)
	m.On("Task").Twice().Return(true, errRetryable)
	m.On("Task").Once().Return(false, nil)

	ctx := context.Background()

	r := retry.ExponentialBackoff{}
	err := r.Start(ctx, "TestRetryUntilSuccess", m.Task)

	require.NoError(t, err)
	m.AssertNumberOfCalls(t, "Task", 3)
}
