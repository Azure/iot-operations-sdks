// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package version

import (
	"strconv"
	"strings"
)

const (
	RPCProtocolString       = "1.0"
	TelemetryProtocolString = "1.0"
)

var (
	RPCSupported       = []int{1}
	TelemetrySupported = []int{1}
)

func ParseProtocol(v string) (major, minor int) {
	if v == "" {
		return 1, 0
	}

	parts := strings.Split(v, ".")
	if len(parts) != 2 {
		return -1, 0
	}

	var err error
	major, err = strconv.Atoi(parts[0])
	if err != nil {
		return -1, 0
	}
	minor, err = strconv.Atoi(parts[1])
	if err != nil {
		return -1, 0
	}
	return major, minor
}

func SerializeSupported(vs string) []int {
	parts := strings.Split(vs, " ")

	res := make([]int, len(parts))
	for i, part := range parts {
		var err error
		res[i], err = strconv.Atoi(part)
		if err != nil {
			return nil
		}
	}
	return res
}

func ParseInt(v []int) string {
	if len(v) == 0 {
		return ""
	}

	res := make([]string, len(v))
	for i, n := range v {
		res[i] = strconv.Itoa(n)
	}
	return strings.Join(res, " ")
}

func IsSupported(v string, supported []int) bool {
	major, _ := ParseProtocol(v)
	for _, s := range supported {
		if major == s {
			return true
		}
	}
	return false
}
