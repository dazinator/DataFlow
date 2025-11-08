# Scenario 003: Dependency-Only Testing - Baseline (No Guidance)

## Context
Testing current workflow guidance when implementing a dependency-only change (no code changes, just package version updates).

## Starting Point
- Copilot agent implementing dependency update
- Only changes: Package versions in .csproj
- No code changes required
- Agent follows Implementation Workflow Step 7 (Validate and Document)

## Steps to Follow (Current Workflow)
1. Update package versions
2. Build succeeds
3. Go to Step 7: Validate and Document
4. Step 7 says "Run all tests"
5. **QUESTION**: Must I run full test suite for dependency-only change?
6. Full test suite takes 5 minutes
7. One pre-existing test fails (unrelated to changes)
8. **CONFUSION**: Step 7 says "Do not fix unrelated failures" but also says "Run all tests"

## Expected Outcome (Baseline)
**Unnecessary work and confusion:**
- Unclear whether full test suite required for dependency-only changes
- Time spent running tests that aren't validating the changes
- Confusion about pre-existing failures
- No guidance on what validation IS required (build? vulnerability scan?)

**Current workflow says:**
- "Run all tests" (seems to apply to all implementations)
- "Do not fix unrelated failures" (but why run them if not fixing?)
- No distinction between code changes vs dependency-only changes

## Success Criteria
- [ ] Workflow requires running all tests for all changes ❌ (overkill)
- [ ] No guidance on dependency-only validation ❌
- [ ] No mention of `dotnet list package --vulnerable` ❌
- [ ] Confusion about pre-existing failures likely ❌

## Test Result
**Status**: BASELINE (showing current gap)

**Notes**:
Current Step 7 validation guidance doesn't distinguish between:
- **Code changes** (require full test suite)
- **Dependency-only changes** (build + vulnerability scan often sufficient)

For dependency-only changes, appropriate validation is:
1. Build verification (ensures no compilation issues)
2. Vulnerability scan (`dotnet list package --vulnerable`)
3. *Optional* test suite if package is production dependency
4. Document any pre-existing failures to avoid confusion

This is especially important for sample/dev-only dependencies where:
- Full test suite not needed
- External dependencies might not be available (e.g., OTLP endpoint)
- Build + vulnerability scan provides sufficient confidence

This validates the need for "Dependency-Only Change Testing Guidance".
