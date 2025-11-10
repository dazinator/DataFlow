# Dependency Update Guide

This guide provides best practices for updating NuGet package dependencies, especially for security fixes and vulnerability remediation.

---

## When to Use This Guide

- Updating NuGet packages to fix security vulnerabilities
- Upgrading package versions for bug fixes or new features
- Resolving NuGet package dependency conflicts
- Maintaining package compatibility across related dependencies

**Note**: This guide covers **NuGet package dependencies** only. For guidance on external runtime dependencies (databases, message queues, OTLP endpoints, etc.) required by sample applications, see:
- Implementation Workflow: `.team/prompts/IMPLEMENTATION_WORKFLOW.md` (Step 0, Step 7)
- Research Workflow: `.team/prompts/RESEARCH_WORKFLOW.md` (Phase 5)
- GitHub issue format for backlog items (issues with `workflow:product-backlog` label)

---

## Dependency Update Pattern

### Check for Related Packages

When updating a NuGet package:

1. **Identify package families**: Check if the package has related packages in the same project (e.g., `OpenTelemetry.*`, `Microsoft.Extensions.*`, `Newtonsoft.Json.*`)
2. **Watch for downgrade warnings**: NuGet package downgrade errors (NU1605) during restore indicate related packages need updating
3. **Check for outdated packages**: Use `dotnet list package --outdated` to identify available updates for related packages
4. **Maintain version consistency**: Update related packages to same major.minor version to avoid compatibility issues

**Example:**
```bash
# Check for outdated packages
dotnet list package --outdated

# Update related packages to same version
# If updating OpenTelemetry.AutoInstrumentation to 1.12.0,
# also update OpenTelemetry.Exporter.*, OpenTelemetry.Extensions.*, etc.
```

---

## Version Selection Guidance

### .NET Framework Version Compatibility

**⚠️ CRITICAL**: Do not update packages to versions that require a newer .NET major version without explicit approval.

**Rule**: If the project targets .NET 8, do not update to package versions that require .NET 9 or later.

**How to check package .NET version requirements:**
1. Check package release notes on NuGet.org or GitHub
2. Look for "Target Framework" or "Dependencies" section
3. Verify package supports current project's target framework (e.g., `net8.0`)

**Example - Checking compatibility:**
```bash
# View package details on NuGet.org
# https://www.nuget.org/packages/<PackageName>/<Version>

# Check Dependencies tab for supported frameworks
# If package only lists net9.0+, it's NOT compatible with net8.0 projects
```

**What to do if latest version requires newer .NET:**
- Use the latest version that still supports the current .NET version
- Document why you're not using the absolute latest version
- Note in PR description: "Using v1.2.3 instead of v2.0.0 (requires .NET 9)"
- If security fix only exists in version requiring newer .NET, escalate to team for decision on framework upgrade

**.NET major version upgrades must be explicitly requested or approved** - do not perform them as part of dependency updates.

### For Security Fixes

- **Always check for latest stable version**, not just the first patched version
- Latest version includes all security patches plus bug fixes and improvements
- **Verify .NET framework compatibility** before selecting version (see above)
- Use NuGet API to list all available versions:
  ```bash
  curl -s "https://api.nuget.org/v3-flatcontainer/<package-name>/index.json" | grep -o '"[0-9]\+\.[0-9]\+\.[0-9]\+"'
  ```
- If handover document specifies minimum patched version, verify if latest is significantly different
- **Prefer latest stable** that is compatible with current .NET version

### For General Updates

- Review release notes for breaking changes
- **Verify .NET framework compatibility** (see above)
- Test incrementally if jumping multiple major versions
- Consider stability requirements (LTS vs current)

---

## Dependency Resolution

### Handling Package Conflicts

- **Package downgrade warnings (NU1605)**: Related packages need updating to satisfy dependency constraints
- **Keep package families synchronized**: Packages like `OpenTelemetry.*` should be at the same major.minor version
- **Test after updates**: Verify compatibility after updating related packages

### Common Patterns

**Pattern 1: Security vulnerability in single package**
```bash
# 1. Check current state
dotnet list package --vulnerable

# 2. Identify affected package and latest version
curl -s "https://api.nuget.org/v3-flatcontainer/<package-name>/index.json"

# 3. Update package
# Edit .csproj file to update version

# 4. Restore and check for downgrade warnings
dotnet restore

# 5. If downgrade warnings, update related packages to same version
# Edit .csproj to update related packages

# 6. Verify fix
dotnet list package --vulnerable
```

**Pattern 2: Multiple related packages to update**
```bash
# 1. List all outdated packages
dotnet list package --outdated

# 2. Update all related packages to same version in .csproj
# Example: All Microsoft.Extensions.* to 8.0.0

# 3. Restore and verify
dotnet restore
dotnet build
```

---

## Validation and Testing

### For Dependency-Only Changes

**Primary Validation** (when ONLY updating package versions, no code changes):

1. **Build verification** - Build must succeed without errors
2. **Vulnerability scan** - Run `dotnet list package --vulnerable` to confirm fix
3. **Warning check** - Verify security warnings (e.g., NU1903) are resolved

**Test Suite Decisions:**
- **Sample/dev-only dependencies**: Build verification sufficient, full test suite optional
- **Production dependencies**: Full test suite required
- **Pre-existing test failures**: Document them to avoid confusion

**Example validation sequence:**
```bash
# 1. Restore packages
dotnet restore

# 2. Check for vulnerabilities
dotnet list package --vulnerable

# 3. Build solution
dotnet build

# 4. For production dependencies, run tests
dotnet test
```

### When External Services Required

Some samples may require external services (databases, OTLP endpoints, message brokers) for runtime validation.

**For guidance on documenting external service requirements:**
- See Implementation Workflow Step 0 for handover review checklist
- See Product Backlog Template for external dependency documentation section
- See Research Workflow Phase 5 for handover creation guidance

**For validation when external services unavailable:**
- Build verification confirms NuGet package compatibility even without runtime validation
- Document in commit message if runtime validation was not performed and why
- If handover documents external dependencies, follow its guidance on validation approach

---

## Common Scenarios

### Scenario 1: Security Vulnerability Fix

**Situation**: NuGet security warning (NU1903) for vulnerable package

**Steps**:
1. Check GitHub Advisory Database or NuGet for vulnerability details
2. Identify patched version (use latest stable)
3. Update package in .csproj
4. Check for and resolve any package downgrade warnings
5. Verify vulnerability is resolved
6. Build and test

**Validation**:
- ✅ No NU1903 warnings in build output
- ✅ `dotnet list package --vulnerable` shows no vulnerabilities
- ✅ Build succeeds
- ✅ Tests pass (for production dependencies)

### Scenario 2: Package Downgrade Conflict

**Situation**: After updating package A, getting NU1605 warnings about package B downgrade

**Steps**:
1. Identify the dependency chain causing the conflict
2. Update package B to compatible version (typically same as package A if in same family)
3. Restore and verify no more warnings
4. Build and test

**Example**:
```
NU1605: Detected package downgrade: OpenTelemetry.Exporter.OpenTelemetryProtocol from 1.12.0 to 1.11.2
  Otel.Example -> OpenTelemetry.AutoInstrumentation 1.12.0 -> OpenTelemetry.Exporter.OpenTelemetryProtocol (>= 1.12.0)
  Otel.Example -> OpenTelemetry.Exporter.OpenTelemetryProtocol (>= 1.11.2)
```

**Fix**: Update `OpenTelemetry.Exporter.OpenTelemetryProtocol` to 1.12.0

### Scenario 3: Package Requires Newer .NET Version

**Situation**: Latest package version requires .NET 9, but project targets .NET 8

**Steps**:
1. Check package release notes/dependencies on NuGet.org
2. Identify latest version that still supports current .NET version (.NET 8)
3. Use that version instead of absolute latest
4. Document in PR why not using latest (e.g., ".NET 9 required")
5. If security fix only in .NET 9 version, escalate for framework upgrade decision

**Example**:
```
Project: net8.0
Package: Microsoft.Extensions.Hosting
- Latest: 9.0.0 (requires .NET 9)
- Use: 8.0.1 (last version supporting .NET 8)

PR Note: "Updated to 8.0.1 (latest .NET 8 compatible). Version 9.0.0 requires .NET 9 upgrade."
```

**Critical**: .NET major version upgrades require explicit approval. Do not perform them as part of dependency updates.

### Scenario 4: Multiple Package Updates

**Situation**: Updating framework version requires updating multiple packages

**Steps**:
1. Use `dotnet list package --outdated` to see all outdated packages
2. **Verify all selected versions support current .NET version**
3. Group packages by family/purpose
4. Update families together to same version
5. Test incrementally if updating many packages
6. Document changes clearly in commit message

---

## Best Practices

### DO
✅ **Verify .NET framework compatibility** before updating packages
✅ Use latest stable version for security fixes (that's compatible with current .NET version)
✅ Update related packages together
✅ Run vulnerability scan after updates
✅ Document why runtime validation wasn't performed (if applicable)
✅ Test production dependencies thoroughly
✅ Check release notes for breaking changes and framework requirements

### DON'T
❌ **Update to package versions requiring newer .NET major version** (e.g., .NET 9 packages in .NET 8 project)
❌ **Upgrade .NET major version** without explicit approval
❌ Use minimum patched version when latest stable is available (unless newer versions require .NET upgrade)
❌ Mix different versions within same package family
❌ Skip vulnerability verification
❌ Ignore package downgrade warnings
❌ Skip testing for production dependencies
❌ Update multiple unrelated packages in same commit (makes rollback harder)

---

## Tools and Commands

### Useful NuGet Commands

```bash
# List all packages
dotnet list package

# List outdated packages
dotnet list package --outdated

# List vulnerable packages
dotnet list package --vulnerable

# List all packages including transitive dependencies
dotnet list package --include-transitive

# Restore packages
dotnet restore

# Build after package updates
dotnet build
```

### Checking Package Versions

```bash
# List all available versions of a package
curl -s "https://api.nuget.org/v3-flatcontainer/<package-name>/index.json"

# Get latest version
curl -s "https://api.nuget.org/v3-flatcontainer/<package-name>/index.json" | grep -o '"[0-9]\+\.[0-9]\+\.[0-9]\+"' | tail -1
```

### GitHub Advisory Database

```bash
# Use the gh-advisory-database tool to check for vulnerabilities
# (Available in Copilot workspace)

# Example usage documented in copilot-instructions.md
```

---

## Related Documentation

- **Implementation Workflow**: `.team/prompts/IMPLEMENTATION_WORKFLOW.md`
  - Step 0: Handover review checklist for dependency updates
  - Step 6: Quick reference to this guide for NuGet updates
  - Step 7: Testing and validation guidance
- **Research Workflow**: `.team/prompts/RESEARCH_WORKFLOW.md`
  - Phase 5: Documenting external dependencies in handovers
- **GitHub Issues**: Backlog items use `workflow:product-backlog` label for dependencies documentation
- **Copilot Instructions**: `.github/copilot-instructions.md`
  - Overall coding standards and practices
- **Security Tools**: See `gh-advisory-database` tool documentation in Copilot workspace

---

## Examples

See the following PRs for examples of dependency updates:
- Security vulnerability fixes in sample applications
- Multi-package family updates
- Handling package downgrade conflicts
