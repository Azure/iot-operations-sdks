# ADR 30: Release Branch Strategy

## Context

The `iot-operations-sdks` repository contains SDK packages for multiple languages (Rust and .NET) that release independently but need to align with the Azure IoT Operations (AIO) release schedule.

### Current State

- Each language has multiple packages (mqtt, protocol, services, connector) that can release independently
- AIO follows a monthly release schedule using YYMM naming (e.g., 2603, 2604)
- Release types alternate between **Milestone** (major features) and **Patch** (bug/vulnerability fixes)

### Problem

The team needs a clear, consistent strategy for:

- When to create release branches
- How release branches are scoped per language
- Where to merge fixes destined for releases
- How release branches relate to the AIO release schedule

## Decision

Adopt a hybrid **tag + release branch** strategy with per-language release branches.

### Branch Structure

| Branch | Purpose |
|--------|---------|
| `main` | Active development branch; always open for new features and fixes |
| `releases/YYMM/<language>` | Per-language release stabilization branch (e.g., `releases/2603/rust`, `releases/2603/dotnet`) |

`main` is a development branch, not a stable branch. It may move ahead of active release branches at any time.

Release branches are scoped per language because each language may require different fixes for the same AIO release cycle.

### Tag Format

Tags mark specific package releases and are scoped by language and package:

**Rust:**

```text
rust/<package>/v<version>
```

Examples: `rust/protocol/v1.0.0`, `rust/services/v1.0.0`, `rust/connector/v1.0.0`

**C#/.NET:**

```text
dotnet/<package>/<version>
```

Examples: `dotnet/protocol/1.0.0`, `dotnet/services/1.0.0`, `dotnet/connector/1.0.0`

The available packages are: `mqtt`, `protocol`, `services`, `connector`.

### Versioning and Release Candidates

During release stabilization, SDK packages go through the following versioning stages:

1. **Beta versions** (e.g., `1.0.0-beta1`, `1.0.0-beta2`) — published at least 2 weeks before code freeze for early integration and validation by consumers
2. **Release candidate** (e.g., `1.0.0-rc1`) — published a few days before code freeze, representing the version that will become the official release
3. **Official release** (e.g., `1.0.0`) — same code as the RC, published on release day

If bugs are found after code freeze, an updated RC is released (e.g., `1.0.0-rc2`). Updates to the RC after code freeze should be strictly bug fixes.

Tags are created for beta, RC, and final releases:

- `rust/services/v1.0.0-beta1`
- `rust/services/v1.0.0-rc1`
- `rust/services/v1.0.0` (final)

### Branch Creation Rules

| Release Type | Create From | When to Create |
|--------------|-------------|----------------|
| **Milestone** (e.g., 2603, 2606) | `main` | When AIO opens the release branch and a release for that language is needed |
| **Patch** (e.g., 2604, 2605) | Previous release branch for that language | When first fix is needed (e.g., `releases/2604/rust` from `releases/2603/rust`) |

### Workflow

#### For Milestone Releases (Major Features)

1. All development merges to `main`
2. When AIO opens the milestone release branch (e.g., 2603) and a release for a language is needed:
   - Create `releases/2603/rust` from `main` (or `releases/2603/dotnet`, etc.)
3. Cherry-pick any additional fixes from `main` to the release branch
4. Build and release packages from the release branch
5. Tag the release (e.g., `rust/protocol/v0.12.0`)

#### For Patch Releases (Bug/Vulnerability Fixes)

1. Fix goes to `main` first
2. When the fix needs to ship in a patch release (e.g., 2604):
   - Create `releases/2604/rust` from `releases/2603/rust` (previous release branch for that language)
   - Cherry-pick the fix from `main`
3. Build and release packages from the release branch
4. Tag the release (e.g., `rust/protocol/v0.12.1`)

#### Backporting Fixes

To backport a fix to a previously released version:

1. Create a new branch off the release tag using the format `release/<language>-<package>-<version>.x` (e.g., `release/rust-services-1.0.x`)
2. The fix goes to `main` first, then is cherry-picked to the new branch
3. This only works if the next patch version is still available (e.g., `v1.0.1` has not been released yet)

### Key Principles

1. **Delay branch creation** — Create release branches as late as possible to minimize cherry-pick overhead
2. **Main is for development** — `main` is the development branch; it may diverge from release branches at any time
3. **Patch branches inherit** — Patch releases branch from the previous release for the same language, not from `main`
4. **Tags mark releases** — Every published release gets a tag on the release branch
5. **Independent releases** — Languages and packages can release at different times within the same AIO release cycle

### Visual Example

```
main ────●────●────●────●────●────●────●────●────●────→
              │                   │
              │                   ├─── releases/2606/rust (milestone)
              │                   └─── releases/2606/dotnet (milestone)
              │
              ├─── releases/2603/rust (milestone)
              │         │
              │         └─── releases/2604/rust (patch)
              │
              └─── releases/2603/dotnet (milestone)
                        │
                        └─── releases/2604/dotnet (patch)
```

## Consequences

### Pros

- **Controlled commits**: Know exactly what goes into each release per language
- **Alignment**: Consistent with the rest of the AIO components release strategy and AIO release schedule
- **Independent releases**: Languages and packages can release on their own schedule without affecting each other
- **Clean main**: Development continues unblocked
- **Traceability**: Tags + branches provide full release history per language

### Cons

- **More branches**: Per-language branches means more branches to track, mitigated by only creating them when a language actually needs to release
- **Cherry-pick overhead**: Patch releases require cherry-picks, though this is typically minimal (1-2 PRs for vulnerability fixes) and milestone releases reset to `main` to avoid long-lived divergence
