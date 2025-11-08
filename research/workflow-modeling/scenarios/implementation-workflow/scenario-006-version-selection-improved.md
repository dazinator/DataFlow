# Scenario 006: Version Selection - Improved (With Guidance)

## Context
Testing improved workflow with "Version Selection Guidance" when implementing a security vulnerability fix.

## Starting Point
- Copilot agent receives handover for CVE fix
- Handover says "Update package X to 1.10.1 (patches CVE-2024-XXXX)"
- Latest version is actually 1.12.0
- Agent follows improved Implementation Workflow

## Steps to Follow (Improved Workflow)
1. Read handover - specifies version 1.10.1
2. **NEW**: Check Implementation Workflow Step 6
3. See reference to NUGET_DEPENDENCY_UPDATES.md for version selection
4. Follow `.team/NUGET_DEPENDENCY_UPDATES.md` "Version Selection Guidance":
   - For security fixes, prefer latest stable version
   - Run: `dotnet list package --outdated` to see available versions
   - Or use NuGet API for comprehensive list
5. See latest is 1.12.0
6. Verify .NET framework compatibility (both support .NET 8)
7. Update to 1.12.0 (includes CVE patch + additional bug fixes)
8. Document decision: "Used latest stable 1.12.0 instead of minimum patched 1.10.1 per NUGET_DEPENDENCY_UPDATES.md guidance"

## Expected Outcome (Improved)
**Consistent, optimal approach:**
- Clear guidance to check for latest version
- Specific commands provided in dedicated guide
- .NET framework compatibility verification included
- Rationale explained (security patches + bug fixes)
- Consistent decisions across implementations
- Better security posture (all patches, not just minimum)

## Success Criteria
- [x] Workflow references NUGET_DEPENDENCY_UPDATES.md for version selection ✅
- [x] Dedicated guide provides comprehensive version selection framework ✅
- [x] Includes .NET framework compatibility checks ✅
- [x] Commands and rationale in one location ✅

## Test Result
**Status**: PASS (with improved guidance)

**Notes**:
With Step 6 referencing NUGET_DEPENDENCY_UPDATES.md, agents get comprehensive version selection guidance including critical .NET framework compatibility checks:

**NUGET_DEPENDENCY_UPDATES.md includes:**
```markdown
### Version Selection Guidance

#### .NET Framework Version Compatibility
**⚠️ CRITICAL**: Do not update packages to versions that require a newer .NET major version

**For Security Fixes:**
- Always check for latest stable version
- Verify .NET framework compatibility before selecting version
- Use latest version that supports current .NET version
- Prefer latest stable over minimum patched (if compatible)

**Commands:**
- `dotnet list package --outdated`
- NuGet API for all versions
- Check package dependencies on NuGet.org
```

Benefits:
- ✅ Better security posture (latest patches)
- ✅ .NET compatibility prevents breaking changes
- ✅ Fewer near-term updates needed
- ✅ Consistent approach across team
- ✅ Comprehensive guide in single location
