# Modernize to File-Scoped Namespaces

**Backlog ID**: techdebt-2025-11-09-file-scoped-namespaces
**Source**: Tech Debt
**Category**: Modern C# Practices
**Status**: Active
**Created**: 2025-11-09
**Updated**: 2025-11-09

## Summary

Convert 290 C# files (92% of codebase) from traditional block-scoped namespaces to modern file-scoped namespaces (C# 10), reducing nesting and removing ~580 lines of code.

## Context

**Source**: Tech debt analysis - `/research/tech-debt-2025-11-09/findings-report.md` (Finding TD-004)
**Priority**: Medium
**Effort**: Large (but automatable)

290 out of 314 C# files (92%) still use traditional block-scoped namespaces instead of modern file-scoped namespaces introduced in C# 10.

Current pattern:
```csharp
namespace Uniun.DataFlow.Blocks
{
    public class MyBlock { }
}
```

Modern pattern:
```csharp
namespace Uniun.DataFlow.Blocks;

public class MyBlock { }
```

The `.editorconfig` has `csharp_style_namespace_declarations = file_scoped:silent` - set to "silent" not "warning", so modernization is not enforced.

**Who is affected**: All developers - more modern, readable code benefits everyone

**Note**: Supersedes existing backlog item `/research/backlog/2025-11-07-modernize-file-scoped-namespaces.md`

## Implementation Guidance

Use automated refactoring tools to convert all namespace declarations to file-scoped, then update .editorconfig to enforce the standard going forward.

### Approach

1. Update .editorconfig to enforce file-scoped namespaces:
   ```
   csharp_style_namespace_declarations = file_scoped:warning
   ```

2. Use automated conversion tool:

   **Option 1: dotnet format (Copilot-friendly)**
   ```bash
   dotnet format --include src/ --include poc/
   ```

   **Option 2: Visual Studio bulk refactoring [Requires Reviewer]**
   - Right-click solution → "Convert to file-scoped namespace"
   - Note: Notify reviewer to perform bulk refactoring, then continue

3. Verify changes:
   ```bash
   dotnet build
   dotnet test
   ```

4. Review a sample of changes manually to ensure quality

### Files Affected

- 290 C# files across `src/` and `poc/` directories
- `src/.editorconfig` - Update namespace style enforcement

### Multi-Phase Implementation

**Recommended**: Single-phase approach
**Rationale**: Fully automatable, purely syntactic change, very low risk. Can convert all files in one PR.

### External Dependencies

No external dependencies required.

## Success Criteria

- [ ] All 290 files converted to file-scoped namespaces
- [ ] .editorconfig updated to enforce file-scoped (warning level)
- [ ] Build succeeds with no new warnings
- [ ] All 180 tests pass
- [ ] ~580 lines removed from codebase (2 per file)
- [ ] Code follows modern .NET conventions
- [ ] Existing documentation covers this functionality (no new docs needed)

## Handover Assets

No additional assets.

## References

- Tech debt analysis: `/research/tech-debt-2025-11-09/findings-report.md` (Finding TD-004)
- Supersedes: `/research/backlog/2025-11-07-modernize-file-scoped-namespaces.md`
- C# file-scoped namespaces: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/namespace

## Notes

**Quick Win**: Large scope but fully automatable = quick win. This is a purely syntactic change with zero behavior impact.

**Benefits**:
- Reduces indentation by one level throughout codebase
- More modern, idiomatic C# code
- Follows .NET coding conventions
- Improved readability with less nesting

**Risk Assessment**: Very low risk - purely syntactic transformation. The compiler ensures no semantic changes.

---

## Status History

- **2025-11-09**: Created from tech debt analysis, supersedes 2025-11-07 backlog item
