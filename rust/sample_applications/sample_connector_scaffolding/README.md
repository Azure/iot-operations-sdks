# Sample Connector Scaffolding

This crate serves as a scaffolding template for starting the development of a
new connector. Note that this template assumes periodic sampling at a fixed
interval. More details can be found in `main.rs`.

## Building a container

Below are instructions for building a container from this crate:

```bash
cargo build --release --target-dir target
docker build -t connector-scaffolding:latest .
```
