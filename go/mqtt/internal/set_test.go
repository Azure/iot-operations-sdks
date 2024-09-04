package internal_test

import (
	"math/rand"
	"sync"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/stretchr/testify/require"
)

func TestSet(t *testing.T) {
	set := internal.NewSet[int]()

	wg := sync.WaitGroup{}
	var mu sync.Mutex
	start := func(i int) {
		defer wg.Done()
		seed := time.Now().UnixNano()
		// #nosec G404
		r := rand.New(rand.NewSource(seed))
		switch r.Intn(3) {
		case 0:
			mu.Lock()
			defer mu.Unlock()
			set.Add(i)
			require.True(t, true, set.Contains(i))
		case 1:
			mu.Lock()
			defer mu.Unlock()
			set.Remove(i)
			require.Equal(t, false, set.Contains(i))
		case 2:
			mu.Lock()
			defer mu.Unlock()
			set.Clear()
			require.Equal(t, 0, set.Size())
		}
	}

	// Start
	for i := 0; i < 100; i++ {
		wg.Add(1)
		go start(i)
	}
	wg.Wait()
}
