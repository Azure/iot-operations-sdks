// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package version

import (
	"strconv"
	"strings"
)

const (
	RPC       = "1.0"
	Telemetry = "1.0"
)

var (
	RPCSupported       = []int{1}
	TelemetrySupported = []int{1}
)

func Parse(v string) (major, minor int) {
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

func ParseSupported(vs string) []int {
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

func SerializeSupported(vs []int) string {
	if len(vs) == 0 {
		return ""
	}

	res := make([]string, len(vs))
	for i, n := range vs {
		res[i] = strconv.Itoa(n)
	}
	return strings.Join(res, " ")
}

func IsSupported(v string, supported []int) bool {
	major, _ := Parse(v)
	for _, s := range supported {
		if major == s {
			return true
		}
	}
	return false
}
