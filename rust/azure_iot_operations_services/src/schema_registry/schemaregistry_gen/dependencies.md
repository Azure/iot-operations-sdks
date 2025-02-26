# Dependencies for the Generated Libraries

Add these dependencies to your project's `Cargo.toml` file:

``` toml
[dependencies]
serde = { version = "1.0", features = ["derive"] }
serde_bytes = "0.11.15"
serde_repr = "0.1"
serde_json = "1.0.105"
chrono = { version = "0.4.31", features = ["serde", "alloc"] }
iso8601-duration = { version = "0.2", features = ["serde", "chrono"] }
base64 = "0.22.1"
bigdecimal = "0.4.5"
time = { version = "0.3", features = ["serde", "formatting", "parsing"] }
uuid = { version = "1.8.0", features = ["serde", "v4"] }
derive_builder = "0.20"
azure_iot_operations_mqtt = { path = "../../../../azure_iot_operations_mqtt" }
azure_iot_operations_protocol = { path = "../../../../azure_iot_operations_protocol" }
```
