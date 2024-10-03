package resp

import (
	"strconv"
)

type Bytes interface{ ~string | ~[]byte }

var separator = []byte{'\r', '\n'}

func appendBlob[T Bytes](data []byte, blob T) []byte {
	data = append(data, '$')
	data = strconv.AppendInt(data, int64(len(blob)), 10)
	data = append(data, separator...)
	data = append(data, blob...)
	data = append(data, separator...)
	return data
}

func OpK[K Bytes](op string, key K, rest ...string) []byte {
	data := strconv.AppendInt([]byte{'*'}, int64(len(rest))+2, 10)
	data = append(data, separator...)

	data = appendBlob(data, op)
	data = appendBlob(data, key)

	for _, arg := range rest {
		data = appendBlob(data, arg)
	}

	return data
}

func OpKV[K, V Bytes](op string, key K, val V, rest ...string) []byte {
	data := strconv.AppendInt([]byte{'*'}, int64(len(rest))+3, 10)
	data = append(data, separator...)

	data = appendBlob(data, op)
	data = appendBlob(data, key)
	data = appendBlob(data, val)

	for _, arg := range rest {
		data = appendBlob(data, arg)
	}

	return data
}
