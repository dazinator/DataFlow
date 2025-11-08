# Scenario 007: Dependency-Only Testing - Improved (With Guidance)

## Context
Testing improved workflow with "Dependency-Only Change Testing Guidance" when implementing a dependency-only update.

## Starting Point
- Copilot agent implementing dependency update
- Only changes: Package versions in .csproj
- No code changes required
- Agent follows improved Implementation Workflow Step 7

## Steps to Follow (Improved Workflow)
1. Update package versions
2. Build succeeds
3. Go to Step 7: Validate and Document
4. **NEW**: See reference to NUGET_DEPENDENCY_UPDATES.md for dependency-only validation
5. Follow `.team/NUGET_DEPENDENCY_UPDATES.md` "Validation and Testing" guidance:
   - Build verification (already done) ✅
   - Run: `dotnet list package --vulnerable` → "No vulnerable packages found" ✅
   - Check package scope: Sample/dev-only dependency
   - Per guide: Build + vulnerability scan sufficient for dev dependencies
   - Note: 1 pre-existing test failure (documented in comments)
6. Document validation approach in commit message per NUGET_DEPENDENCY_UPDATES.md examples
7. Complete with confidence - appropriate validation done

## Expected Outcome (Improved)
**Efficient, appropriate validation:**
- Clear reference to comprehensive validation guide
- Appropriate level of testing for change type
- No confusion about pre-existing failures
- Time saved (2 minutes vs 7+ minutes with unnecessary full suite)
- Documented rationale for validation approach

## Success Criteria
- [x] Workflow references NUGET_DEPENDENCY_UPDATES.md for validation ✅
- [x] Dedicated guide distinguishes dependency-only from code changes ✅
- [x] Guide provides streamlined validation steps ✅
- [x] Mentions `dotnet list package --vulnerable` ✅
- [x] Guidance on when full test suite is optional ✅
- [x] Addresses pre-existing failure confusion ✅

## Test Result
**Status**: PASS (with improved guidance)

**Notes**:
With Step 7 referencing NUGET_DEPENDENCY_UPDATES.md, agents get comprehensive validation guidance:

**Implementation Workflow Step 7:**
```markdown
**For NuGet package dependency updates**: See [Dependency Update Guide](../../.team/NUGET_DEPENDENCY_UPDATES.md) 
section on "Validation and Testing" for specific guidance on:
- When build verification is sufficient vs full test suite
- Vulnerability scanning with `dotnet list package --vulnerable`
- Handling pre-existing test failures
- Production vs dev-only dependency testing approaches
```

**NUGET_DEPENDENCY_UPDATES.md provides:**
```markdown
### For Dependency-Only Changes

**Primary Validation:**
1. Build verification - Build must succeed
2. Vulnerability scan - Run `dotnet list package --vulnerable`
3. Warning check - Verify security warnings resolved

**Test Suite Decisions:**
- Sample/dev-only dependencies: Build verification sufficient
- Production dependencies: Full test suite required
- Pre-existing test failures: Document them

**Example validation sequence and commit message included**
```

Benefits:
- ✅ Saves time (appropriate validation, not excessive)
- ✅ Reduces confusion about test failures
- ✅ Clear criteria for production vs dev dependencies
- ✅ Documents validation approach for reviewers
- ✅ Single comprehensive guide for all validation scenarios
