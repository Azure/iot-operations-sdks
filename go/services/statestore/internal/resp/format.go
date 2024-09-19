package resp

import "strconv"

var separator = []byte{'\r', '\n'}

func FormatBlob(blob string) []byte {
	data := strconv.AppendInt([]byte{'$'}, int64(len(blob)), 10)
	data = append(data, separator...)
	data = append(data, blob...)
	data = append(data, separator...)
	return data
}

func FormatBlobArray(ary ...string) []byte {
	data := strconv.AppendInt([]byte{'*'}, int64(len(ary)), 10)
	data = append(data, separator...)
	for _, blob := range ary {
		data = append(data, FormatBlob(blob)...)
	}
	return data
}
