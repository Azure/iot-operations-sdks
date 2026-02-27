# Akri Connector Framework Versioning & Package Pinning Strategy

## Overview

The Akri Connector Framework is distributed as a set of NuGet packages that provide shared infrastructure for historian connectors. This document describes the versioning strategy, how connectors should pin framework versions, and the CI/CD workflow for publishing packages.

## Package Architecture

The framework consists of four packable libraries:

| Package | Purpose | Key Dependencies |
|---------|---------|------------------|
| `Akri.ConnectorFramework.Abstractions` | Core contracts and interfaces | None (stable base) |
| `Akri.ConnectorFramework.Host` | Hosting infrastructure, lifecycle, observability | Abstractions, Microsoft.Extensions.* |
| `Akri.RqrAdapter` | ResilientQueryRunner integration | Abstractions, ResilientQueryRunner |
| `Akri.IotOps` | Azure IoT Operations integration | Abstractions, AIO SDK |

## Semantic Versioning

All framework packages follow [Semantic Versioning 2.0.0](https://semver.org/):

```
MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]
```

### Version Components

- **MAJOR**: Breaking changes to public APIs or behavior
  - Example: Removing or renaming interface methods, changing checkpoint format
  - Connectors must update code to adopt a new major version

- **MINOR**: New features, backwards-compatible additions
  - Example: New optional configuration settings, additional extension points
  - Connectors can adopt without code changes

- **PATCH**: Bug fixes, performance improvements
  - Example: Fixing a memory leak, correcting metrics calculation
  - Connectors should adopt routinely

- **PRERELEASE**: Alpha, beta, preview, rc (release candidate)
  - Format: `-preview.N`, `-alpha.N`, `-beta.N`, `-rc.N`
  - Used before stable releases
  - No compatibility guarantees between prerelease versions

### Version Progression Example

```
0.1.0-preview.1  → Initial preview
0.1.0-preview.2  → Preview bug fixes
0.1.0-rc.1       → Release candidate
0.1.0            → First stable release
0.1.1            → Patch: bug fix
0.2.0            → Minor: new feature
1.0.0            → Major: breaking change
```

## Connector Package Pinning

### Strategy: Exact Version Pinning

**Connectors MUST pin exact framework package versions** to ensure predictable behavior and avoid unexpected breaking changes.

#### Recommended Approach

Use exact version numbers in connector project files:

```xml
<ItemGroup>
  <!-- Pin exact framework versions for stability -->
  <PackageReference Include="Akri.ConnectorFramework.Abstractions" Version="0.1.0-preview.1" />
  <PackageReference Include="Akri.ConnectorFramework.Host" Version="0.1.0-preview.1" />
  <PackageReference Include="Akri.RqrAdapter" Version="0.1.0-preview.1" />
  <PackageReference Include="Akri.IotOps" Version="0.1.0-preview.1" />
</ItemGroup>
```

#### Why Exact Versions?

1. **Predictability**: Connector behavior is deterministic across builds
2. **Stability**: Avoids surprise breakage from framework updates
3. **Testing**: Validates connector with specific framework version
4. **Deployment**: Production deployments are reproducible
5. **Rollback**: Can revert to previous known-good versions

### Updating Framework Versions

When updating framework packages, connectors should:

1. **Review Release Notes**: Check for breaking changes, new features, bug fixes
2. **Update All Together**: Keep framework package versions aligned
3. **Test Thoroughly**: Validate connector behavior after upgrade
4. **Document Changes**: Note framework version in connector release notes

#### Update Example

```bash
# Update all framework packages to 0.2.0
dotnet add package Akri.ConnectorFramework.Abstractions --version 0.2.0
dotnet add package Akri.ConnectorFramework.Host --version 0.2.0
dotnet add package Akri.RqrAdapter --version 0.2.0
dotnet add package Akri.IotOps --version 0.2.0

# Build and test
dotnet build
dotnet test
```

### Version Compatibility

Framework packages are designed to work together at matching versions:

- ✅ **Supported**: All framework packages at same version (e.g., all 0.1.0)
- ⚠️ **Use Caution**: Mixed versions within same MAJOR.MINOR (e.g., 0.1.0 + 0.1.1)
- ❌ **Not Supported**: Mixed versions across MAJOR or MINOR boundaries

## CI/CD Pack & Publish Workflow

### Local Packing

Use the provided pack script to create NuGet packages locally:

```powershell
# Pack all framework libraries with default version from csproj
.\build\pack.ps1

# Pack with explicit version (e.g., CI build)
.\build\pack.ps1 -Version "0.1.0-preview.2"

# Output packages to custom directory
.\build\pack.ps1 -OutputDir "C:\packages"
```

Packages are created in `build/packages/` by default:

```
build/packages/
├── Akri.ConnectorFramework.Abstractions.0.1.0-preview.1.nupkg
├── Akri.ConnectorFramework.Abstractions.0.1.0-preview.1.snupkg
├── Akri.ConnectorFramework.Host.0.1.0-preview.1.nupkg
├── Akri.ConnectorFramework.Host.0.1.0-preview.1.snupkg
├── Akri.RqrAdapter.0.1.0-preview.1.nupkg
├── Akri.RqrAdapter.0.1.0-preview.1.snupkg
├── Akri.IotOps.0.1.0-preview.1.nupkg
└── Akri.IotOps.0.1.0-preview.1.snupkg
```

### CI Build Integration

For continuous integration builds:

```yaml
# Example GitHub Actions workflow
- name: Pack Framework Libraries
  run: |
    # Calculate version from tag or commit
    $version = "0.1.0-preview.${{ github.run_number }}"
    .\build\pack.ps1 -Version $version
  
- name: Publish to NuGet
  run: |
    dotnet nuget push build/packages/*.nupkg \
      --source https://api.nuget.org/v3/index.json \
      --api-key ${{ secrets.NUGET_API_KEY }} \
      --skip-duplicate
```

### Version Sources

The pack script supports multiple version sources:

1. **Explicit Parameter**: `-Version "1.2.3"` (highest priority)
2. **Git Tag**: Reads from annotated tag if on tagged commit
3. **Project File**: Falls back to `<Version>` in .csproj
4. **Default**: Uses `0.0.1-dev` if no version found

### Publishing Strategy

#### Prerelease Workflow (0.x versions)

1. Develop features on feature branch
2. Pack with `-preview.N` suffix
3. Publish to NuGet as prerelease
4. Test in connector projects
5. Stabilize and release as `0.x.0`

#### Stable Release Workflow (1.0+)

1. Release candidate: `1.0.0-rc.1`
2. Testing period (2+ weeks)
3. Stable release: `1.0.0`
4. Patch releases as needed: `1.0.1`, `1.0.2`

## Version Coordination

### Development Phase (0.x)

- Framework packages evolve rapidly
- Breaking changes allowed in minor versions
- Connectors update frequently during development
- Coordinate framework + connector releases

### Stable Phase (1.0+)

- Breaking changes only in major versions
- Minor versions are backwards-compatible
- Connectors can adopt new minors without code changes
- Patch versions recommended for all connectors

## Best Practices

### For Framework Developers

1. **Document Breaking Changes**: Clearly mark in release notes
2. **Deprecation Period**: Mark APIs obsolete before removal
3. **Migration Guides**: Provide upgrade paths for major versions
4. **Test Compatibility**: Validate against existing connectors
5. **Changelog**: Maintain detailed changelog per package

### For Connector Developers

1. **Pin Exact Versions**: Never use version ranges
2. **Update Deliberately**: Plan and test framework upgrades
3. **Keep Versions Aligned**: All framework packages at same version
4. **Document Framework Version**: Note in connector README
5. **Test After Upgrade**: Full validation after framework update

## Troubleshooting

### Version Conflicts

**Problem**: Build errors about version conflicts

```
error NU1605: Detected package downgrade: Akri.ConnectorFramework.Abstractions from 0.2.0 to 0.1.0
```

**Solution**: Align all framework package versions in connector project:

```xml
<!-- Update all to 0.2.0 -->
<PackageReference Include="Akri.ConnectorFramework.Abstractions" Version="0.2.0" />
<PackageReference Include="Akri.ConnectorFramework.Host" Version="0.2.0" />
```

### Missing Package Versions

**Problem**: NuGet can't find framework package version

**Solution**: Check package availability on NuGet.org or ensure local package source is configured:

```bash
dotnet nuget list source
dotnet nuget add source "build/packages" --name "Local"
```

### Breaking Changes

**Problem**: Connector breaks after framework update

**Solution**: Review release notes, check for API changes, update connector code:

1. Read framework CHANGELOG.md for breaking changes
2. Update connector to match new API signatures
3. Test connector with new framework version
4. Consider staying on previous major version if upgrade is complex

## References

- [Semantic Versioning 2.0.0](https://semver.org/)
- [NuGet Package Versioning](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning)
- [.NET Package Version Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- Framework Release Notes: See `CHANGELOG.md` in each package directory
