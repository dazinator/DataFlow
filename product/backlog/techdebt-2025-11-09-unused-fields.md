# Remove Unused Fields

**Backlog ID**: techdebt-2025-11-09-unused-fields
**Source**: Tech Debt
**Category**: Code Quality / Cleanup
**Status**: Active
**Created**: 2025-11-09
**Updated**: 2025-11-09

## Summary

Remove 4 unused private fields flagged by CS0169 warnings to clean up dead code and eliminate compiler warnings.

## Context

**Source**: Tech debt analysis - `/research/tech-debt-2025-11-09/findings-report.md` (Finding TD-008)
**Priority**: Low
**Effort**: Small

4 instances of CS0169: "The field is never used"

Known instances:
- `RoutingBlockTests._serviceProvider`
- `BatchBlockTests._serviceProvider`
- 2 other instances

These are likely leftover fields from refactoring that are no longer needed.

**Who is affected**: Developers maintaining these test classes

## Implementation Guidance

Review each unused field, verify it's truly not needed, then remove it.

### Approach

For each CS0169 warning:

1. Locate the unused field in source code
2. Search for any references (should find none)
3. Verify in git history why it was added (if unclear)
4. Determine if it was meant to be used:
   - **If truly unused**: Remove the field
   - **If should be used**: Add the missing usage
5. Run tests to ensure no regressions

### Files Affected

- `src/Tests/DataFlow/RoutingBlockTests.cs`
- `src/Tests/DataFlow/BatchBlockTests.cs`
- 2 other test files (identify from build warnings)

### External Dependencies

No external dependencies required.

## Success Criteria

- [ ] All CS0169 warnings eliminated (4 warnings)
- [ ] Unused fields removed or properly utilized
- [ ] All tests pass (180 tests)
- [ ] No dead code remains
- [ ] Build output cleaner
- [ ] Existing documentation covers this functionality (no new docs needed)

## Handover Assets

No additional assets.

## References

- Tech debt analysis: `/research/tech-debt-2025-11-09/findings-report.md` (Finding TD-008)
- CS0169 warning: https://learn.microsoft.com/en-us/dotnet/csharp/misc/cs0169

## Notes

**Quick Win**: Takes ~10 minutes to identify and remove unused fields. Very low risk.

**Benefits**:
- Cleaner codebase
- Less confusion for developers
- Reduced compiler warnings

**Risk Assessment**: Very low risk - removing dead code that's not being used.

**Verification**: Git blame can help understand why fields were added originally, ensuring they're truly unused and not meant for future use.

---

## Status History

- **2025-11-09**: Created from tech debt analysis
