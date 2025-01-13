// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package version

import (
	"strconv"
	"strings"
)

const (
	ProtocolString  = "1.0"
	SupportedString = "1"
)

var Supported = ParseSupported(SupportedString)

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

func ParseSupported(vs string) []int {
	parts := strings.Split(vs, " ")
	if len(parts) == 0 {
		return nil
	}

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

func IsSupported(v string) bool {
	major, _ := ParseProtocol(v)
	for _, s := range Supported {
		if major == s {
			return true
		}
	}
	return false
}
