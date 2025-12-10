# Central Package Management Guide

This solution uses **centralized package version management** via `Directory.Packages.props` to ensure consistent dependency versions across all projects.

---

## When to Use This Guide

Use this guide when:
- Adding new NuGet packages to the solution
- Updating existing package versions
- Resolving package version conflicts
- Understanding why builds fail with "PackageVersion" errors

**Related Guide**: For updating packages (especially security fixes), also see [NuGet Dependency Updates Guide](./nuget_dependency_updates.md).

---

## How Central Package Management Works

### The System

**Central Version Definition**:
- All package versions are defined in `src/Directory.Packages.props`
- Single source of truth for all package versions
- Version updates happen in one central location

**Project References**:
- Project files (`*.csproj`) reference packages WITHOUT specifying versions
- Projects automatically use the centrally-defined version
- No version conflicts across projects

### Example

**Directory.Packages.props**:
```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageVersion Include="Shouldly" Version="4.2.1" />
    <PackageVersion Include="xUnit" Version="2.6.2" />
  </ItemGroup>
</Project>
```

**Project.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <!-- No Version attribute - uses central definition -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Shouldly" />
  </ItemGroup>
</Project>
```

---

## Adding New Packages

Follow this workflow when adding a new package dependency:

### Step 1: Check if Package Already Exists

```bash
# Search central package list
grep "PackageVersion Include=\"<package-name>\"" src/Directory.Packages.props
```

**If found**: Skip to Step 4 (reference in project)

**If not found**: Continue to Step 2

---

### Step 2: Check if Available Transitively

Many packages come as transitive dependencies (automatically included by other packages).

**How to check**:
```bash
# Build the project
dotnet build

# Look for messages like:
# "Package '<package-name>' is already defined by a transitive dependency"
```

**If transitive**:
- Package is already available
- No need to add explicit reference
- Use it directly in code

**If not transitive**: Continue to Step 3

---

### Step 3: Add to Central Management

Edit `src/Directory.Packages.props`:

```xml
<ItemGroup>
  <!-- Add new package with version -->
  <PackageVersion Include="NewPackageName" Version="1.2.3" />
</ItemGroup>
```

**Version Selection Guidance**:
- Use latest stable version compatible with target framework (.NET 8)
- Check release notes for breaking changes
- **⚠️ CRITICAL**: Verify package supports .NET 8 - do not add packages requiring .NET 9+

**Alphabetical Ordering**:
- Keep `PackageVersion` entries alphabetically sorted within groups
- Makes finding packages easier
- Reduces merge conflicts

---

### Step 4: Reference in Project

Edit the project's `.csproj` file:

```xml
<ItemGroup>
  <!-- Reference WITHOUT version attribute -->
  <PackageReference Include="NewPackageName" />
</ItemGroup>
```

**Important**: 
- ❌ Do NOT include `Version` attribute in project file
- ✅ Version comes from central definition
- Build will fail if you specify version in project

---

### Step 5: Verify

```bash
# Restore packages
dotnet restore

# Build to verify
dotnet build
```

**Expected**: Build succeeds without errors

**If errors**: See Troubleshooting section below

---

## Common Patterns

### Pattern 1: Test Packages

Most test packages are already centrally defined:

**Already Available**:
- xUnit, xUnit.runner.visualstudio
- Shouldly (assertion library)
- Moq (mocking framework)
- FluentAssertions
- Microsoft.NET.Test.Sdk

**To Use**:
```xml
<!-- In test project .csproj -->
<ItemGroup>
  <PackageReference Include="xUnit" />
  <PackageReference Include="Shouldly" />
  <PackageReference Include="Moq" />
</ItemGroup>
```

---

### Pattern 2: Microsoft.Extensions.* Packages

Microsoft.Extensions packages should use the same major.minor version:

**Example**:
```xml
<!-- All at version 8.0.x -->
<PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
<PackageVersion Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
```

**Rationale**: These packages are designed to work together at the same version.

---

### Pattern 3: Package Families

Keep related package families at consistent versions:

**Examples**:
- `OpenTelemetry.*` packages → same version
- `System.Threading.*` packages → same version
- `Newtonsoft.Json.*` packages → same version

**Why**: Reduces version conflicts and ensures compatibility.

---

## Updating Package Versions

### For Security Updates

See [NuGet Dependency Updates Guide](./nuget_dependency_updates.md) for complete security update workflow, including:
- Checking for vulnerable packages
- Selecting patched versions
- Resolving dependency conflicts
- Validation and testing

---

### For General Updates

**Step 1: Check Outdated Packages**
```bash
dotnet list package --outdated
```

**Step 2: Review Release Notes**
- Check for breaking changes
- Verify .NET version compatibility
- Review new features / bug fixes

**Step 3: Update Central Definition**

Edit `src/Directory.Packages.props`:
```xml
<!-- Before -->
<PackageVersion Include="PackageName" Version="1.0.0" />

<!-- After -->
<PackageVersion Include="PackageName" Version="1.1.0" />
```

**Step 4: Test**
```bash
dotnet restore
dotnet build
dotnet test
```

**Note**: Single change in `Directory.Packages.props` updates ALL projects.

---

## Troubleshooting

### Error: "Version is not allowed on this item"

**Symptom**:
```
error NU1008: Projects that use central package version management should not define the version on the PackageReference items but on the PackageVersion items
```

**Cause**: Project `.csproj` has a version specified:
```xml
<!-- INCORRECT -->
<PackageReference Include="SomePackage" Version="1.0.0" />
```

**Fix**: Remove version from project file:
```xml
<!-- CORRECT -->
<PackageReference Include="SomePackage" />
```

---

### Error: "Package ... is not defined"

**Symptom**:
```
error NU1009: The package 'SomePackage' is not defined in Directory.Packages.props
```

**Cause**: Package referenced in project but not in central definition.

**Fix**: Add to `src/Directory.Packages.props`:
```xml
<PackageVersion Include="SomePackage" Version="x.y.z" />
```

---

### Error: Package Downgrade Warning (NU1605)

**Symptom**:
```
warning NU1605: Detected package downgrade: Package.Name from x.y.z to x.y.w
```

**Cause**: Related packages at different versions causing conflict.

**Fix**: Update related packages to same version in `Directory.Packages.props`.

**Example**:
```xml
<!-- If updating OpenTelemetry.AutoInstrumentation to 1.12.0 -->
<!-- Also update related packages -->
<PackageVersion Include="OpenTelemetry.AutoInstrumentation" Version="1.12.0" />
<PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.12.0" />
<PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.12.0" />
```

---

### Error: "Package requires .NET 9"

**Symptom**:
```
error NU1202: Package PackageName x.y.z is not compatible with net8.0
```

**Cause**: Package version requires newer .NET framework than project targets.

**Fix**: 
1. Find latest version compatible with .NET 8:
   ```bash
   # Check package dependencies on NuGet.org
   # Look for versions supporting net8.0
   ```

2. Use compatible version:
   ```xml
   <!-- Use version that supports net8.0 -->
   <PackageVersion Include="PackageName" Version="a.b.c" />
   ```

3. Document in commit message why not using latest:
   ```
   Updated PackageName to a.b.c (latest .NET 8 compatible)
   Note: Version x.y.z requires .NET 9 upgrade
   ```

**⚠️ CRITICAL**: Do not upgrade .NET major version without explicit approval.

---

### Duplicate Package Definitions

**Symptom**: Same package defined multiple times in `Directory.Packages.props`

**Fix**: Remove duplicate, keep single definition:
```xml
<!-- INCORRECT - duplicates -->
<PackageVersion Include="PackageName" Version="1.0.0" />
...
<PackageVersion Include="PackageName" Version="1.1.0" />

<!-- CORRECT - single definition -->
<PackageVersion Include="PackageName" Version="1.1.0" />
```

---

## .NET Version Compatibility

### Critical Rule

**⚠️ DO NOT** add package versions requiring newer .NET major version without explicit approval.

**Current Framework**: .NET 8

**Allowed**: Packages supporting `net8.0`
**Not Allowed**: Packages requiring `net9.0` or later

---

### How to Check Compatibility

**Method 1: NuGet.org**
1. Go to https://www.nuget.org/packages/{PackageName}/{Version}
2. Click "Dependencies" tab
3. Check "Target Framework" list
4. Verify `net8.0` or compatible framework

**Method 2: Release Notes**
- Check package GitHub repository
- Review release notes for framework requirements
- Look for "Breaking Changes" or "Requirements" sections

**Method 3: API Check**
```bash
# Get package metadata
curl -s "https://api.nuget.org/v3-flatcontainer/{package}/index.json"

# Check specific version details
curl -s "https://api.nuget.org/v3-flatcontainer/{package}/{version}/{package}.nuspec"
```

---

### What to Do When Latest Requires .NET 9+

**Scenario**: Latest package version requires .NET 9, but project targets .NET 8.

**Action**:
1. Identify latest .NET 8 compatible version
2. Use that version instead
3. Document in PR description:
   ```
   Using PackageName v8.0.x (latest .NET 8 compatible)
   Note: v9.0.0 requires .NET 9 framework upgrade
   ```

4. If security fix only in .NET 9 version:
   - Document the issue
   - Escalate for decision on framework upgrade
   - Do not perform .NET upgrade as part of dependency update

**Example**:
```xml
<!-- Project targets net8.0 -->
<!-- Latest: 9.0.0 (requires .NET 9) -->
<!-- Using: 8.0.1 (latest .NET 8 compatible) -->
<PackageVersion Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
```

---

## Why Central Package Management?

### Benefits

**Version Consistency**:
- All projects use same package versions
- No version conflicts between projects
- Single source of truth

**Simplified Updates**:
- Update version in one place
- All projects automatically use new version
- Easier to maintain

**Reduced Merge Conflicts**:
- Changes isolated to single file
- Alphabetical ordering reduces conflicts
- Clear ownership of version decisions

**Better Security**:
- Easy to scan for vulnerable versions
- Update once, fixes everywhere
- Audit trail in git history

---

### Trade-offs

**Flexibility**:
- Cannot have different versions per project
- Some projects may not need latest version
- Workaround: Use explicit version in special cases (discouraged)

**Learning Curve**:
- Different from traditional NuGet usage
- Build errors may be unfamiliar
- Requires understanding of system

**Migration**:
- Existing projects need migration
- One-time effort to centralize
- Worth it for long-term maintainability

---

## Best Practices

### DO

✅ Check for existing package before adding
✅ Check for transitive dependencies
✅ Keep package families at same version
✅ Verify .NET framework compatibility before adding
✅ Use latest stable version (compatible with .NET 8)
✅ Update related packages together
✅ Keep alphabetically sorted
✅ Document .NET version constraints in commit messages

---

### DON'T

❌ Add version attribute in project `.csproj` files
❌ Add packages requiring .NET 9+ to .NET 8 project
❌ Upgrade .NET major version without approval
❌ Mix versions within package families
❌ Add package without checking if transitive
❌ Use pre-release versions in production code
❌ Skip testing after version updates
❌ Create duplicate `PackageVersion` entries

---

## Quick Reference

### Check Package Status
```bash
# List all packages
dotnet list package

# List outdated
dotnet list package --outdated

# List vulnerable
dotnet list package --vulnerable

# Search central definition
grep "PackageVersion Include=\"PackageName\"" src/Directory.Packages.props
```

### Add New Package
```bash
# 1. Check if exists
grep "PackageVersion Include=\"NewPackage\"" src/Directory.Packages.props

# 2. Add to Directory.Packages.props
# <PackageVersion Include="NewPackage" Version="x.y.z" />

# 3. Reference in project .csproj
# <PackageReference Include="NewPackage" />

# 4. Verify
dotnet restore && dotnet build
```

### Update Package Version
```bash
# 1. Edit src/Directory.Packages.props
# Change: Version="1.0.0" to Version="1.1.0"

# 2. Test
dotnet restore
dotnet build
dotnet test
```
