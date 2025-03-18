## Retry policies
All SDKs use retry policy of exponential back off with jitter. The name of the classes are only included for reference.
| Language | C# | Rust | Go |
|----------|------|------|------|
| **Name** | ExponentialBackoffRetryPolicy | ExponentialBackoffWithJitter | ExponentialBackoff |
| **Attributes** | • maxRetries: Maximum number of<br>  retry attempts<br>• baseExponent: Starting exponent<br>  for backoff calculation<br>• maxDelay: Maximum time between<br>  retries<br>• useJitter: Whether to randomize<br>  delay times | • max_wait: Maximum time<br>  between retries<br>• max_reconnect_attempts: Maximum<br>  number of retry attempts<br>• MIN_EXPONENT: Minimum exponent<br>  for calculations<br>• BASE_DELAY_MS: Base value for<br>  exponential calculation | • MaxAttempts: Maximum number<br>  of retry attempts<br>• MinInterval: Starting delay<br>  between retries<br>• MaxInterval: Maximum delay<br>  between retries<br>• Timeout: Total timeout for all<br>  retries combined<br>• NoJitter: Flag to disable jitter<br>• Logger: Logger for operations |
| **Default Values** | • maxRetries: unlimited<br>• baseExponent: 6<br>• maxDelay: 60s<br>• useJitter: true | • max_wait: 60s<br>• max_reconnect_attempts: None<br>• MIN_EXPONENT: 7<br>• BASE_DELAY_MS: 2 | • MaxAttempts: 0 (unlimited)<br>• MinInterval: 125ms (1/8s)<br>• MaxInterval: 60s<br> |
| **Jitter Range** | 95%-105% of calculated delay | 90%-100% of calculated delay | 95%-105% of calculated delay |
| **Used In** | • LeaderElectionClient<br>• LeasedLockClient<br>• MqttSessionClient | • MQTT Session reconnection<br>  logic | • MQTT SessionClient|



## Code Usages

| Client | Parameters Passed | Description |
|--------|-------------------|-------------|
| **C# - LeaderElectionClient** | `new ExponentialBackoffRetryPolicy(`<br>`  _retryPolicyMaxRetries,  // 5`<br>`  _retryPolicyBaseExponent,  // 1`<br>`  _retryPolicyMaxWait  // 200ms`<br>`)` | Used with leader election for retry on lock acquisition |
| **C# - LeasedLockClient** | `new ExponentialBackoffRetryPolicy(`<br>`  _retryPolicyMaxRetries,  // 5`<br>`  _retryPolicyBaseExponent,  // 1`<br>`  _retryPolicyMaxWait  // 200ms`<br>`)` | Used for lock acquisition with more aggressive retry parameters |
| **C# - MqttSessionClient** | `new ExponentialBackoffRetryPolicy(`<br>`  uint.MaxValue,  // Unlimited retries`<br>`  TimeSpan.FromSeconds(60)  // 60s max delay`<br>`)` | Used for MQTT session reconnection with unlimited retries |
| **Rust - Session Connection** | `ExponentialBackoffWithJitter {`<br>`  max_wait: Duration::from_secs(60),`<br>`  max_reconnect_attempts: None,`<br>`}` | Used for MQTT session reconnection with unlimited retries |
| **Go - SessionClient** | `&retry.ExponentialBackoff{`<br>`  Logger: client.options.Logger,`<br>`}` |Used for MQTT session reconnection with unlimited retries |


