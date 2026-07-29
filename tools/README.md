# Tools

This directory contains various tools used to deploy and develop on Azure IoT Operations.

## SDK version policy for tools

Tools live in this repo for convenience, but they are **not** part of the SDK.
They are independently compiled and distributed as binaries or container
images, on their own lifecycle, and they consume the AIO SDK the same way any
external customer does — from published crates.io releases.

That means a tool tracks a **milestone release** (currently `2607`), not the
tip of `main`:

| crate | 2607 |
|---|---|
| `azure_iot_operations_protocol` | `1.0.1` |
| `azure_iot_operations_mqtt` | `1.1.0` |
| `azure_iot_operations_services` | `1.3.0` |

Two rules follow from that, and both are load-bearing:

1. **Each tool crate commits its `Cargo.lock`.** The repo-wide `.gitignore`
   ignores lockfiles because that is correct for the SDK's library crates; the
   tool crates are un-ignored explicitly. Cargo requirements are caret ranges,
   so without a lock a tool silently resolves to the newest compatible `1.x`
   at build time. Downstream consumers pin this repo by commit hash for
   reproducibility — the lockfile is what makes that pin mean anything, since
   a commit hash otherwise freezes only the source, not the dependencies.

2. **Every build of a tool passes `--locked`.** CI does this unconditionally in
   `_rust-tool-check.yml`, and image builds do it in their Dockerfile. Without
   it Cargo just refreshes a stale lock in place, so a manifest bump that
   forgot to regenerate the lock would pass CI and only fail later in a
   downstream image build. A new tool crate that lands without a committed lock
   fails that job outright, which is intended — see rule 1.

Moving a tool to a newer release is a deliberate act, not something that
should happen on its own — if a tool works, there is no need to keep updating
it. To move one:

```sh
cd tools/<tool>
# edit Cargo.toml to the target release versions, then:
cargo update -p azure_iot_operations_mqtt --precise <version>
cargo update -p azure_iot_operations_protocol --precise <version>
cargo build --locked --release   # must succeed before committing
```

Commit the `Cargo.toml` and `Cargo.lock` changes together.
