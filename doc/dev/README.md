# Developing the Azure IoT Operations SDKs

Documentation in this folder is intended for development of the Azure IoT Operations SDKs and associated tooling, and aren't required for developing Azure IoT Operations applications with the SDKs.

For documentation on using the Azure IoT Operations SDKs for developing applications at the edge, refer to [Azure IoT Operations SDKs documentation](/doc).

## Resources

* [Packaging the SDKs](packaging.md) - How to create and push the SDK packages to the ADO feed

## Rust CI warning policy

The [Rust CI workflow](../../.github/workflows/ci-rust.yml) runs `make check` on the
[pinned development toolchain](../../rust/rust-toolchain.toml), which currently
matches the [SDK MSRV](../../rust/Cargo.toml). Clippy explicitly uses
`--deny=warnings` there. Standalone Rust tools likewise deny Clippy warnings on
their own pinned toolchains.

The SDK test matrix also runs on current stable Rust. New compiler warnings must
not fail otherwise unrelated changes, so Rust setup steps explicitly set both
`rustflags: ''` and `build-warnings: ''`. This disables action-level warning
denial, including the v2 action's default `CARGO_BUILD_WARNINGS=deny` on Cargo
1.97+, without suppressing warnings or relaxing explicit lint rules.
