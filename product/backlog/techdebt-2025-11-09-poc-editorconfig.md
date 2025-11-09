# Add .editorconfig to POC Projects

**Backlog ID**: techdebt-2025-11-09-poc-editorconfig
**Source**: Tech Debt
**Category**: Code Quality / Consistency
**Status**: Active
**Created**: 2025-11-09
**Updated**: 2025-11-09

## Summary

Add .editorconfig file to POC folder to ensure consistent code style across entire repository (both production and POC code).

## Context

**Source**: Tech debt analysis - `/research/tech-debt-2025-11-09/findings-report.md` (Finding TD-006)
**Priority**: Medium
**Effort**: Small

Production code (`/src`) has comprehensive .editorconfig (27KB, well-configured) enforcing C# style guidelines. POC code (`/poc`) has no .editorconfig, leading to potential style inconsistencies between production and POC code.

This creates an uneven development experience where:
- Production code has IDE warnings/suggestions for style violations
- POC code has no style enforcement
- Contributors may write inconsistent code in POC

**Who is affected**: All developers working on POC code

## Implementation Guidance

Copy or link the production .editorconfig to POC folder to ensure consistent styling across both codebases.

### Approach

**Option 1: Copy to POC** (Recommended)
```bash
cp src/.editorconfig poc/.editorconfig
```

**Option 2: Root-level .editorconfig**
- Move .editorconfig to repository root
- Both src/ and poc/ will inherit from it
- Only if the same rules apply to both codebases

**Option 3: POC-specific with inheritance**
- Create poc/.editorconfig that inherits from src/.editorconfig
- Override specific rules if POC needs different conventions

### Files Affected

- New file: `poc/.editorconfig`
- Or moved: `.editorconfig` (to root)

### External Dependencies

No external dependencies required.

## Success Criteria

- [ ] POC projects have .editorconfig enforcement
- [ ] Consistent style rules between src/ and poc/
- [ ] IDE shows style warnings for POC code
- [ ] No build failures introduced
- [ ] POC code follows same conventions as production code
- [ ] Existing documentation covers this functionality (no new docs needed)

## Handover Assets

No additional assets.

## References

- Tech debt analysis: `/research/tech-debt-2025-11-09/findings-report.md` (Finding TD-006)
- Current src/.editorconfig: `/home/runner/work/lib-dataflow/lib-dataflow/src/.editorconfig`

## Notes

**Quick Win**: Takes ~5 minutes to copy file and verify. Immediate consistency improvement.

**Benefits**:
- Consistent code style across entire repository
- Better IDE support in POC projects
- Easier code review between POC and production
- Clear coding standards for all contributors

**Risk Assessment**: Very low risk - just adds configuration, no code changes.

**Future Consideration**: If POC needs different style rules than production, can customize the POC .editorconfig while keeping base conventions aligned.

---

## Status History

- **2025-11-09**: Created from tech debt analysis
