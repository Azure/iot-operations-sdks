package retrypolicy

import "time"

const (
	defaultMaxInterval = 30 * time.Second
	defaultWithJitter  = true
)
