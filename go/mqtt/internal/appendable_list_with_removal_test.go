package internal

import (
	"testing"

	"github.com/stretchr/testify/require"
)

func TestIterateInOrder(t *testing.T) {
	list := NewAppendableListWithRemoval[int]()

	// append integers in the range [0, 5)
	for i := range 5 {
		_ = list.AppendEntry(i)
	}

	// retrieve values from list and put them into a slice
	var actual []int
	for v := range list.Iterator() {
		actual = append(actual, v)
	}

	expected := []int{0, 1, 2, 3, 4}
	require.Equal(t, expected, actual)
}

func TestRemoveAtEnd(t *testing.T) {
	list := NewAppendableListWithRemoval[int]()

	// append integers in the range [0, 4)
	for i := range 4 {
		_ = list.AppendEntry(i)
	}
	// append 4
	removeEnd := list.AppendEntry(4)
	// remove from the end
	removeEnd()

	// retrieve values from list and put them into a slice
	var actual []int
	for v := range list.Iterator() {
		actual = append(actual, v)
	}

	expected := []int{0, 1, 2, 3}
	require.Equal(t, expected, actual)
}

func TestRemoveAtBeginning(t *testing.T) {
	list := NewAppendableListWithRemoval[int]()

	// append 0
	removeBeginning := list.AppendEntry(0)
	// append integers in the range [1, 5)
	for i := 1; i < 5; i++ {
		_ = list.AppendEntry(i)
	}
	//remove from the beginning
	removeBeginning()

	// retrieve values from list and put them into a slice
	var actual []int
	for v := range list.Iterator() {
		actual = append(actual, v)
	}

	expected := []int{1, 2, 3, 4}
	require.Equal(t, expected, actual)
}

func TestRemoveInMiddle(t *testing.T) {
	list := NewAppendableListWithRemoval[int]()

	// append values
	_ = list.AppendEntry(0)
	_ = list.AppendEntry(1)
	removeMiddle := list.AppendEntry(2)
	_ = list.AppendEntry(3)
	_ = list.AppendEntry(4)

	//remove from the middle
	removeMiddle()

	// retrieve values from list and put them into a slice
	actual := []int{}
	for v := range list.Iterator() {
		actual = append(actual, v)
	}

	expected := []int{0, 1, 3, 4}
	require.Equal(t, expected, actual)
}

func TestIterateEmpty(t *testing.T) {
	list := NewAppendableListWithRemoval[int]()
	for _ = range list.Iterator() {
		t.Error("iterator unexpectedly yielded a value")
		break
	}
}

func TestIterateRandomRemoval(t *testing.T) {
	// Two randomly generated but fixed shuffles (to ensure determinism)
	testCases := [][]int{
		{9, 4, 7, 1, 3, 5, 2, 0, 8, 6},
		{8, 3, 9, 6, 2, 0, 1, 4, 5, 7},
	}

	for _, tc := range testCases {
		list := NewAppendableListWithRemoval[int]()
		var removalFuncs []func()

		// append integers in the range [0, 4)
		for i := range 10 {
			removalFuncs = append(removalFuncs, list.AppendEntry(i))
		}

		// remove all elements in the order determined by the shuffle
		for _, shuffleIdx := range tc {
			removalFuncs[shuffleIdx]()
		}

		for _ = range list.Iterator() {
			t.Error("iterator unexpectedly yielded a value")
			break
		}
	}

}
