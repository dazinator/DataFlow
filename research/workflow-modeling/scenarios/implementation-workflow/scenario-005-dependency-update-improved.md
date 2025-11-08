# Scenario 005: Dependency Update - Improved (With Guidance)

## Context
Testing improved Implementation Workflow with "Dependency Update Pattern" guidance when implementing a dependency update with package conflicts.

## Starting Point
- Copilot agent receives handover issue for security vulnerability fix
- Handover says "Update OpenTelemetry.Instrumentation.AspNetCore to 1.10.1"
- Agent follows Implementation Workflow Step 6

## Steps to Follow (Improved Workflow)
1. Read handover document
2. Update package version as specified
3. Run `dotnet restore`
4. See error: "NU1605: Detected package downgrade: OpenTelemetry.Api from 1.10.0 to 1.9.0"
5. **NEW**: Check workflow Step 6 for dependency guidance
6. See reference to NUGET_DEPENDENCY_UPDATES.md guide
7. Follow `.team/NUGET_DEPENDENCY_UPDATES.md` "Dependency Update Pattern":
   - Recognize this is a related package scenario (OpenTelemetry.*)
   - Run `dotnet list package --outdated` to find related packages
   - Update all OpenTelemetry.* packages to same major version
   - Rerun `dotnet restore` → Success!

## Expected Outcome (Improved)
**Clear, efficient resolution:**
- Agent encounters error
- Workflow references comprehensive dependency guide
- Agent follows detailed patterns in NUGET_DEPENDENCY_UPDATES.md
- Resolves conflict quickly using documented steps
- Consistent approach across all implementations

## Success Criteria
- [x] Workflow references NUGET_DEPENDENCY_UPDATES.md for NuGet updates ✅
- [x] Dedicated guide provides detailed patterns and commands ✅
- [x] Proper separation of concerns (workflow vs detailed guide) ✅
- [x] Clear navigation path to comprehensive guidance ✅

## Test Result
**Status**: PASS (with improved guidance)

**Notes**:
With Step 6 now referencing `.team/NUGET_DEPENDENCY_UPDATES.md`, agents have:

**Implementation Workflow Step 6:**
```markdown
### NuGet Package Dependency Updates

**For NuGet package updates** (security fixes, version updates, dependency conflicts):

📖 **See [Dependency Update Guide](../../.team/NUGET_DEPENDENCY_UPDATES.md)** for comprehensive guidance on:
- Dependency update patterns and package families
- Version selection (security fixes, .NET compatibility)
- Handling package conflicts and downgrade warnings
- Validation and testing approaches
- Common scenarios and best practices

**Quick reference for common tasks:**
- Check for outdated packages: `dotnet list package --outdated`
- Check for vulnerabilities: `dotnet list package --vulnerable`
- See `.team/NUGET_DEPENDENCY_UPDATES.md` for detailed patterns
```

**Benefits of this approach:**
- ✅ Single source of truth for NuGet dependency guidance
- ✅ Comprehensive patterns without cluttering workflow
- ✅ Easy to maintain and update (one location)
- ✅ Includes .NET framework compatibility guidance
- ✅ Proper document separation and linking

Estimated time savings: 15-30 minutes per dependency update with conflicts.
